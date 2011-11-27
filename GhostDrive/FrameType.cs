using System;
using Microsoft.SPOT;

namespace GhostDrive
{
    /// <summary>
    /// The types of frames that can be sent over XBee
    /// </summary>
    enum FrameType : byte
    {
        NoteOn = 0x10,
        NoteOff = 0x11,
        Enable = 0x12,
        Disable = 0x13,
        SetModulation = 0x20,
        SetLocation = 0x21,
        ResetLocation = 0x22,
        PlaySong = 0x30,
        StopSong = 0x31,
    }
}
