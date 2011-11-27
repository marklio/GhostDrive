using System;
using Microsoft.SPOT;
using System.IO.Ports;

namespace GhostDrive
{
    class RemoteManager
    {

        SerialPort _Port;
        byte[] _SendBuffer = new byte[] { 0x65, 0, 0 }; //magic, type, data
        byte[] _ReceiveBuffer = new byte[3];

        enum FrameType : byte
        {
            NoteOn = 0x10,
            NoteOff = 0x11,
            Enable = 0x12,
            Disable = 0x13,
            SetModulation = 0x20,
            SetLocation = 0x21,
            ResetLocation = 0x22,
        }

        public RemoteManager(SerialPort serialPort)
        {
            _Port = serialPort;
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
