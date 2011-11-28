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
using Midi;
using System.Collections;

namespace GhostDrive
{
    public class Program
    {
        static bool _LedState = false;

        static OutputPort _Led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, _LedState);

        static IFloppySynth _LocalSynth = new FloppySynth(FEZ_Pin.Digital.IO43, PWM.Pin.PWM4, FEZ_Pin.Digital.IO41, FEZ_Pin.Digital.IO2, 1);
        static RemoteManager _RemoteManager = new RemoteManager(new SerialPort("COM4", 115200, Parity.None, 8, StopBits.One));
        static IFloppySynth _RemoteSynth = _RemoteManager.RemoteSynth;
        static string _SDRoot = null;

        class SynthInfo
        {
            public int OctaveModulation = 0;
            public ushort TrackId = 0;
        }

        public static void Main()
        {
            _RemoteManager.OnPlaySong += (songNumber) =>
            {
                if (_SDRoot == null) return;
                PlaySong(Path.Combine(_SDRoot, songNumber + ".song"));
            };
            _RemoteManager.OnStopSong += () =>
            {
                StopSong();
            };

            //Hook the insert event
            //TODO: deal with eject/insert more robustly?
            RemovableMedia.Insert += (s, e) =>
            {
                Util.DebugPrint("Found SD card");
                _LocalSynth.Disable();
                _RemoteSynth.Disable();

                if (e.Volume.IsFormatted)
                {
                    //List the songs
                    Util.DebugPrint("Songs:");
                    _SDRoot = e.Volume.RootDirectory;
                    var files = Directory.GetFiles(_SDRoot);
                    var pq = new PriorityQueue(files.Length, x => byte.Parse(Path.GetFileNameWithoutExtension(x.ToString())));
                    foreach (var file in files)
                    {
                        if (Path.GetExtension(file).ToLower() == ".song")
                        {
                            Util.DebugPrint(file);
                            pq.Push(file);
                        }
                    }
                    //Play the songs
                    while (!pq.IsEmpty) {
                        var songFile = pq.Pop() as string;
                        PlaySong(songFile);
                        //PlaySong is async, only play the first one.
                        break;
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

        static Thread _PlayThread;

        private static void PlaySong(string songFile)
        {
            StopSong();
            _PlayThread = new Thread(() =>
            {
                string file;
                SynthInfo[] trackMap = new SynthInfo[2];//this needs to match the number of synths we know about
                using (var songReader = new StreamReader(File.OpenRead(songFile)))
                {
                    file = Path.Combine(_SDRoot, songReader.ReadLine());
                    string line;
                    while ((line = songReader.ReadLine()) != null)
                    {
                        //it seems like ReadLine is stupid in NETMF
                        if (line.Length == 0) break;
                        var elements = line.Split(',');
                        var synth = byte.Parse(elements[0]);
                        trackMap[synth] = new SynthInfo
                        {
                            TrackId = ushort.Parse(elements[1]),
                            OctaveModulation = elements.Length > 2 ? int.Parse(elements[2]) : 0,
                        };
                    }
                }
                Debug.Print(file);
                Util.DebugPrint("Playing " + file);
                var midi = new MidiFile(file);
                var lastTime = 0u;
                midi.NoteOn += evt =>
                {
                    var timeToWait = new TimeSpan((evt.Time - lastTime) * midi.Tempo * TimeSpan.TicksPerMillisecond / 1000 / midi.TicksPerBeat);
                    Util.DebugPrint("Time To Wait: " + timeToWait.ToString());
                    Thread.Sleep((int)(timeToWait.Ticks / TimeSpan.TicksPerMillisecond));
                    Util.DebugPrint("NOTE ON: " + evt.Note.ToString());
                    lastTime = evt.Time;
                    if (evt.TrackId == trackMap[1].TrackId)
                        _RemoteSynth.PlayNote(evt.Note);
                    if (evt.TrackId == trackMap[0].TrackId)
                        _LocalSynth.PlayNote(evt.Note);
                };
                midi.NoteOff += evt =>
                {
                    var timeToWait = new TimeSpan((evt.Time - lastTime) * midi.Tempo * TimeSpan.TicksPerMillisecond / 1000 / midi.TicksPerBeat);
                    Util.DebugPrint("Time To Wait: " + timeToWait.ToString());
                    Thread.Sleep((int)(timeToWait.Ticks / TimeSpan.TicksPerMillisecond));
                    Util.DebugPrint("NOTE OFF: " + evt.Note.ToString());
                    lastTime = evt.Time;
                    if (evt.TrackId == trackMap[1].TrackId)
                        _RemoteSynth.StopNote();
                    if (evt.TrackId == trackMap[0].TrackId)
                        _LocalSynth.StopNote();
                };
                _LocalSynth.OctaveModulation = trackMap[0].OctaveModulation;
                _RemoteSynth.OctaveModulation = trackMap[1].OctaveModulation;
                _LocalSynth.Enable();
                _RemoteSynth.Enable();
                midi.Play();
                _LocalSynth.Disable();
                _RemoteSynth.Disable();
                Util.DebugPrint("Done playing");
            });
            _PlayThread.Start();
        }

        private static void StopSong()
        {
            //TODO: This isn't reasonable.  .Play needs to be nicely interruptable
            if (_PlayThread != null && _PlayThread.IsAlive) _PlayThread.Abort();
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
