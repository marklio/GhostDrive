using System;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.IO;
using Microsoft.SPOT.IO;
using System.IO;
using System.IO.Ports;

namespace GhostDrive
{
    public class Program
    {
        static bool _LedState = false;

        static OutputPort _Led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, _LedState);

        static IFloppySynth _LocalSynth = new FloppySynth(FEZ_Pin.Digital.IO43, PWM.Pin.PWM4, FEZ_Pin.Digital.IO41, FEZ_Pin.Digital.IO2, 1);
        static RemoteManager _RemoteManager = new RemoteManager(new SerialPort("COM4", 115200, Parity.None, 8, StopBits.One));
        static IFloppySynth _RemoteSynth = _RemoteManager.RemoteSynth;

        public static void Main()
        {
            _LocalSynth.OctaveModulation = -1;
            _RemoteSynth.OctaveModulation = -1;

            //Hook the insert event
            //TODO: deal with eject/insert more robustly?
            RemovableMedia.Insert += (s, e) =>
            {
                Util.DebugPrint("Found SD card");
                _LocalSynth.Disable();

                if (e.Volume.IsFormatted)
                {
                    //List the songs
                    Util.DebugPrint("Songs:");
                    var files = Directory.GetFiles(e.Volume.RootDirectory);
                    foreach (var file in files)
                    {
                        if (Path.GetExtension(file).ToLower() == ".mid")
                        {
                            Util.DebugPrint(file);
                        }
                    }
                    //Play the songs
                    //TODO: figure out the right triggers for this
                    foreach (var file in files)
                    {
                        Util.DebugPrint("Playing " + file);
                        var parser = new MIDIFileParser();
                        parser.ParseFile(file);
                        _LocalSynth.Enable();
                        Util.DebugPrint("Playing " + parser.NoteCount + " note events");
                        for (int i = 0; i < parser.NoteCount; i++)
                        {
                            MIDIFileParser.NoteEvent noteEvent = parser.NoteEvents[i];
                            TimeSpan timeToWait = new TimeSpan(noteEvent.DeltaTime * parser.Tempo * TimeSpan.TicksPerMillisecond / 1000 / parser.TicksPerBeat);
                            Util.DebugPrint("Time To Wait: " + timeToWait.ToString());
                            Thread.Sleep((int)(timeToWait.Ticks / TimeSpan.TicksPerMillisecond));

                            if ((noteEvent.EventType & 0xF0) == 0x80)
                            {
                                // Note Off
                                Util.DebugPrint("NOTE OFF: " + noteEvent.NoteNumber.ToString());
                                _LocalSynth.StopNote();
                                _Led.Write(false);
                            }
                            else if ((noteEvent.EventType & 0xF0) == 0x90)
                            {
                                // Note On
                                Util.DebugPrint("NOTE ON: " + noteEvent.NoteNumber.ToString());
                                _LocalSynth.PlayNote(noteEvent.NoteNumber);
                                _Led.Write(true);
                            }
                        }
                        _LocalSynth.Disable();
                        Util.DebugPrint("Done playing");
                    }
                }
                else
                {
                    Util.DebugPrint("SD Card not formatted!");
                }
            };
            RemovableMedia.Eject += (s, e) =>
            {
                Util.DebugPrint("SD card removed");
            };

            //loop till we find the SD card
            while (true)
            {
                if (HasStableSDCard())
                {
                    var sdCard = new PersistentStorage("SD");
                    //Mount the filesystem (this will cause the insert event to fire
                    sdCard.MountFileSystem();
                    break;
                }
                else
                {
                    Util.DebugPrint("No SD card");
                    //TODO: some indication?
                    Thread.Sleep(5000);
                }
            }
            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// This indicates that the presence of the SD card is "stable"
        /// </summary>
        /// <returns></returns>
        static bool HasStableSDCard()
        {
            if (PersistentStorage.DetectSDCard())
            {
                Thread.Sleep(50);
                return PersistentStorage.DetectSDCard();
            }
            return false;
        }
    }
}
