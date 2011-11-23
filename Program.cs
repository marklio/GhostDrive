using System;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.FEZ;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.IO;
using Microsoft.SPOT.IO;
using System.IO;

namespace GhostDrive
{
    public class Program
    {

        static bool _LedState = false;

        static OutputPort _Led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, _LedState);

        static FloppySynth _LocalSynth = new FloppySynth(FEZ_Pin.Digital.IO43, PWM.Pin.PWM4, FEZ_Pin.Digital.IO41, FEZ_Pin.Digital.IO2, 1) { OctaveModulation = -1 };

        public static void Main()
        {
            //Hook the insert event
            //TODO: deal with eject/insert more robustly?
            RemovableMedia.Insert += (s, e) =>
            {
                Debug.Print("Found SD card");
                _LocalSynth.Disable();

                if (e.Volume.IsFormatted)
                {
                    //List the songs
                    Debug.Print("Songs:");
                    var files = Directory.GetFiles(e.Volume.RootDirectory);
                    foreach (var file in files)
                    {
                        if (Path.GetExtension(file).ToLower() == ".mid")
                        {
                            Debug.Print(file);
                        }
                    }
                    //Play the songs
                    //TODO: figure out the right triggers for this
                    foreach (var file in files)
                    {
                        Debug.Print("Playing " + file);
                        var parser = new MIDIFileParser();
                        parser.ParseFile(file);
                        _LocalSynth.Enable();
                        Debug.Print("Playing " + parser.NoteCount + " note events");
                        for (int i = 0; i < parser.NoteCount; i++)
                        {
                            MIDIFileParser.NoteEvent noteEvent = parser.NoteEvents[i];
                            TimeSpan timeToWait = new TimeSpan(noteEvent.DeltaTime * parser.Tempo * TimeSpan.TicksPerMillisecond / 1000 / parser.TicksPerBeat);
                            Debug.Print("Time To Wait: " + timeToWait.ToString());
                            Thread.Sleep((int)(timeToWait.Ticks / TimeSpan.TicksPerMillisecond));

                            if ((noteEvent.EventType & 0xF0) == 0x80)
                            {
                                // Note Off
                                Debug.Print("NOTE OFF: " + noteEvent.NoteNumber.ToString());
                                _LocalSynth.StopNote();
                            }
                            else if ((noteEvent.EventType & 0xF0) == 0x90)
                            {
                                // Note On
                                Debug.Print("NOTE ON: " + noteEvent.NoteNumber.ToString());
                                _LocalSynth.PlayNote(noteEvent.NoteNumber);
                            }
                        }
                        _LocalSynth.Disable();
                        Debug.Print("Done playing");
                    }
                }
                else
                {
                    Debug.Print("SD Card not formatted!");
                }
            };
            RemovableMedia.Eject += (s, e) =>
            {
                Debug.Print("SD card removed");
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
                    Debug.Print("No SD card");
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
