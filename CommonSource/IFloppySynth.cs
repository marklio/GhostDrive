using System;
namespace GhostDrive
{
    /// <summary>
    /// Interface for a floppy drive synthesizer
    /// </summary>
    interface IFloppySynth
    {
        /// <summary>
        /// Controls moduliation of note events by octaves
        /// (a floppy drive's range is on the low side)
        /// </summary>
        int OctaveModulation { get; set; }
        /// <summary>
        /// Disable the synth (this also turns off the drive's LED
        /// </summary>
        void Disable();
        /// <summary>
        /// Enables the synth (this also turns on the drive's LED
        /// </summary>
        void Enable();
        /// <summary>
        /// Begins emitting the specified MIDI note (or changes a currently
        /// playing note to the specified MIDI note)
        /// </summary>
        /// <param name="note"></param>
        void PlayNote(int note);
        /// <summary>
        /// Resets the head's location to the last set location
        /// </summary>
        void ResetLocation();
        /// <summary>
        /// Sets the location of the head
        /// </summary>
        /// <param name="trackLocation"></param>
        void SetLocation(byte trackLocation);
        /// <summary>
        /// Stops emitting any current note (or a no-op in the case
        /// of no current note)
        /// </summary>
        void StopNote();
    }
}
