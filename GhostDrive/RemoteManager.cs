using System;
using Microsoft.SPOT;
using System.IO.Ports;
using System.Threading;
using XBee;

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
        //SerialPort _Port;
        byte[] _SendBuffer = new byte[] { 0x65, 0, 0 }; //magic, type, data
        byte[] _ReceiveBuffer = new byte[3];
        //Thread _ReadThread;
        XBeeDevice xbee;

        public XBeeDevice XBee { get { return xbee; } }

        public delegate void NoteOnHandler(byte note);
        public delegate void NoteOffHandler();
        public delegate void EnableHandler();
        public delegate void DisableHandler();
        public delegate void SetModulationHandler(sbyte modulation);
        public delegate void SetLocationHandler(byte location);
        public delegate void ResetLocationHandler();
        public delegate void PlaySongHandler(byte songNumber);
        public delegate void StopSongHandler();
        public delegate void SaveSongHandler(byte songNumber);
        public delegate void RandomWalkHandler(int seed);

        public event NoteOnHandler OnNoteOn;
        public event NoteOffHandler OnNoteOff;
        public event EnableHandler OnEnable;
        public event DisableHandler OnDisable;
        public event SetModulationHandler OnSetModulation;
        public event SetLocationHandler OnSetLocation;
        public event ResetLocationHandler OnResetLocation;
        public event PlaySongHandler OnPlaySong;
        public event StopSongHandler OnStopSong;
        public event SaveSongHandler OnSaveSong;
        public event RandomWalkHandler OnRandomWalk;

        public RemoteManager(SerialPort serialPort)
        {
            xbee = new XBeeDevice(serialPort);
            xbee.FrameReceived += frame => {
                if (frame.CommandId != CommandId.RxPacket)
                    return false;

                var buffer = frame.Buffer;
                if (buffer[11] != 0x65)
                    return false;

                switch ((FrameType)buffer[12]) {
                case FrameType.NoteOn:
                    NoteOn(buffer[13]);
                    break;
                case FrameType.NoteOff:
                    NoteOff();
                    break;
                case FrameType.Enable:
                    Enable();
                    break;
                case FrameType.Disable:
                    Disable();
                    break;
                case FrameType.SetModulation:
                    SetModulation((sbyte)buffer[13]);
                    break;
                case FrameType.SetLocation:
                    SetLocation(buffer[13]);
                    break;
                case FrameType.ResetLocation:
                    ResetLocation();
                    break;
                case FrameType.PlaySong:
                    PlaySong(buffer[13]);
                    break;
                case FrameType.StopSong:
                    StopSong();
                    break;
                case FrameType.SaveSong:
                    SaveSong(buffer[13]);
                    break;
                case FrameType.RandomWalk:
                    var seed = buffer[13] << 24 | buffer[14] << 16 | buffer[15] << 8 | buffer[16];
                    RandomWalk(seed);
                    break;
                }
                return true;
            };
            xbee.Open();
        }

        void NoteOn(byte note) {
            var onNoteOn = OnNoteOn;
            if (onNoteOn != null)
                onNoteOn(note);
        }

        void NoteOff() {
            var onNoteOff = OnNoteOff;
            if (onNoteOff != null)
                onNoteOff();
        }

        void Enable() {
            var onEnable = OnEnable;
            if (onEnable != null)
                onEnable();
        }

        void Disable() {
            var onDisable = OnDisable;
            if (onDisable != null)
                onDisable();
        }

        void SetModulation(sbyte modulation) {
            var onSetModulation = OnSetModulation;
            if (onSetModulation != null)
                onSetModulation(modulation);
        }

        void SetLocation(byte location) {
            var onSetLocation = OnSetLocation;
            if (onSetLocation != null)
                onSetLocation(location);
        }

        void ResetLocation() {
            var onResetLocation = OnResetLocation;
            if (onResetLocation != null)
                onResetLocation();
        }

        void PlaySong(byte songNumber) {
            var onPlaySong = OnPlaySong;
            if (onPlaySong != null)
                onPlaySong(songNumber);
        }

        void StopSong() {
            var onStopSong = OnStopSong;
            if (onStopSong != null)
                onStopSong();
        }

        void SaveSong(byte songNumber) {
            var onSaveSong = OnSaveSong;
            if (onSaveSong != null)
                onSaveSong(songNumber);
        }

        void RandomWalk(int seed) {
            var onRandomWalk = OnRandomWalk;
            if (onRandomWalk != null)
                onRandomWalk(seed);
        }
    }
}
