using System;
using Microsoft.SPOT;
using System.IO.Ports;
using System.Threading;

namespace GhostDrive
{
    /// <summary>
    /// Handles all remote communications for the device
    /// </summary>
    /// <remarks>
    /// The device currently sends data to the slave device,
    /// making the controller unable to tell if it is working.
    /// TODO: consider implementing API mode on the device to
    /// allow richer communications.
    /// </remarks>
    class RemoteManager
    {
        SerialPort _Port;
        byte[] _SendBuffer = new byte[] { 0x65, 0, 0 }; //magic, type, data
        byte[] _ReceiveBuffer = new byte[3];
        Thread _ReadThread;

        public delegate void PlaySongHandler(byte songNumber);
        public delegate void StopSongHandler();

        public event PlaySongHandler OnPlaySong;
        public event StopSongHandler OnStopSong;

        public RemoteManager(SerialPort serialPort)
        {
            _Port = serialPort;
            if (!_Port.IsOpen) _Port.Open();
            _ReadThread = new Thread(() =>
            {
                var readBuffer = new byte[3];
                while (true)
                {
                    if (_Port.BytesToRead >= 3)
                    {
                        _Port.Read(readBuffer, 0, 3);
                        if (readBuffer[0] != 0x65)
                        {
                            _Port.Read(readBuffer, 0, 1);
                            continue;
                        }
                        switch ((FrameType)readBuffer[1])
                        {
                            case FrameType.PlaySong:
                                var songNumber = readBuffer[2];
                                PlaySong(songNumber);
                                break;
                            case FrameType.StopSong:
                                StopSong();
                                break;
                        }
                    }
                    else
                    {
                        Thread.Sleep(50);
                    }
                }
            });
            _ReadThread.Start();
        }

        private void StopSong()
        {
            var onStopSong = OnStopSong;
            if (onStopSong != null) onStopSong();
        }

        private void PlaySong(byte songNumber)
        {
            var onPlaySong = OnPlaySong;
            if (onPlaySong != null) onPlaySong(songNumber);
        }

        void SendFrame(FrameType type)
        {
            SendFrame(type, 0);
        }

        void SendFrame(FrameType type, byte data)
        {
            _SendBuffer[1] = (byte)type;
            _SendBuffer[2] = data;
            _Port.Write(_SendBuffer, 0, 3);
        }

        IFloppySynth _RemoteSynth;
        public IFloppySynth RemoteSynth
        {
            get
            {
                return _RemoteSynth ?? (_RemoteSynth = new RemoteFloppySynth(this));
            }
        }

        class RemoteFloppySynth : IFloppySynth
        {
            RemoteManager _Manager;
            public RemoteFloppySynth(RemoteManager manager)
            {
                _Manager = manager;
            }

            int _OctaveModulation = 0;
            public int OctaveModulation
            {
                get { return _OctaveModulation; }
                set { SetOctaveModulation(value); }
            }

            private void SetOctaveModulation(int octaveModulation)
            {
                _OctaveModulation = octaveModulation;
                //TODO: under/overflow?
                _Manager.SendFrame(FrameType.SetModulation, (byte)octaveModulation);
            }

            public void ResetLocation()
            {
                _Manager.SendFrame(FrameType.ResetLocation);
            }

            public void SetLocation(byte trackLocation)
            {
                _Manager.SendFrame(FrameType.SetLocation, trackLocation);
            }

            public void Enable()
            {
                _Manager.SendFrame(FrameType.Enable);
            }

            public void Disable()
            {
                _Manager.SendFrame(FrameType.Disable);
            }

            public void PlayNote(int note)
            {
                //TODO: overflow?
                _Manager.SendFrame(FrameType.NoteOn, (byte)note);
            }

            public void StopNote()
            {
                _Manager.SendFrame(FrameType.NoteOff);
            }
        }
    }
}
