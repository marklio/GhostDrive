using System;
namespace GhostDrive
{
    interface IFloppySynth
    {
        int OctaveModulation { get; set; }
        void Disable();
        void Enable();
        void PlayNote(int note);
        void ResetLocation();
        void SetLocation(byte trackLocation);
        void StopNote();
    }
}
