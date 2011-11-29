using System;
using Microsoft.SPOT;
using GHIElectronics.NETMF.Hardware;
using GHIElectronics.NETMF.FEZ;
using Microsoft.SPOT.Hardware;
using System.Threading;

namespace GhostDrive
{
    /// <summary>
    /// Implements a floppy disk synthesizer
    /// </summary>
    class FloppySynth : IFloppySynth
    {
        PWM _Step;
        OutputPort _Dir;
        InterruptPort _Interrupt;
        OutputPort _Disable;
        byte _TrackLocation;
        Timer _RunawayNoteTimer;
        const int RUNAWAY_TIMEOUT = 5000; //timeout notes after 5 seconds


        public int OctaveModulation { get; set; }

        /// <summary>
        /// Creates a floppy synth instance
        /// </summary>
        /// <param name="disablePin">The pin connected to the /enable signal</param>
        /// <param name="stepPin">The PWM-capable pin connected to the /step signal</param>
        /// <param name="interruptPin">The interrupt-capable IO pin connected to the /step signal (to drive the dir pin)</param>
        /// <param name="dirPin">The PWM-capable pin connected to the dir signal</param>
        /// <param name="trackLocation"></param>
        public FloppySynth(FEZ_Pin.Digital disablePin, PWM.Pin stepPin, FEZ_Pin.Digital interruptPin, FEZ_Pin.Digital dirPin, byte trackLocation)
        {
            _RunawayNoteTimer = new Timer((o) =>
            {
                StopNote();
            }, null, -1, -1);
            var dirState = false;
            _Interrupt = new InterruptPort((Cpu.Pin)interruptPin, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeHigh);
            _Step = new PWM(stepPin);
            _Dir = new OutputPort((Cpu.Pin)dirPin, dirState);
            //when the interrupt is triggered, toggle the direction
            _Interrupt.OnInterrupt += (d1, d2, t) =>
            {
                dirState = !dirState;
                _Dir.Write(dirState);
            };
            _Interrupt.EnableInterrupt();
            _Disable = new OutputPort((Cpu.Pin)disablePin, true);
            SetLocation(trackLocation);
        }

        /// <summary>
        /// Puts the head back at the track location previously set
        /// </summary>
        public void ResetLocation()
        {
            SetLocation(_TrackLocation);
        }

        /// <summary>
        /// Sets the track location of the head
        /// </summary>
        /// <param name="trackLocation"></param>
        public void SetLocation(byte trackLocation)
        {
            //prevent the interrupt from firing and screwing up our location
            _Interrupt.DisableInterrupt();
            Enable();
            _TrackLocation = trackLocation;
            //just for fun we run it back and forth just to see something happen.
            _Dir.Write(false);
            for (int i = 0; i < 90; i++)
            {
                _Step.Set(true);
                Thread.Sleep(10);
                _Step.Set(false);
                Thread.Sleep(1);
            }
            _Dir.Write(true);
            for (int i = 0; i < 90; i++)
            {
                _Step.Set(true);
                Thread.Sleep(10);
                _Step.Set(false);
                Thread.Sleep(1);
            }
            _Dir.Write(false);
            for (int i = 0; i < _TrackLocation; i++)
            {
                _Step.Set(true);
                Thread.Sleep(10);
                _Step.Set(false);
                Thread.Sleep(1);
            }
            Disable();
            _Interrupt.EnableInterrupt();
        }

        /// <summary>
        /// Disable the synth
        /// </summary>
        public void Disable()
        {
            StopNote();
            _Disable.Write(true);
        }

        /// <summary>
        /// Enable the synth
        /// </summary>
        public void Enable()
        {
            _Disable.Write(false);
        }

        /// <summary>
        /// Begins playing a MIDI note
        /// </summary>
        /// <param name="note"></param>
        public void PlayNote(int note)
        {
            var frequency = GetFrequencyForNote(note) * System.Math.Pow(2, OctaveModulation);
            var period = 1.0/frequency; //seconds
            var nanoseconds = (uint)(period * 1000000000);
            //TODO: what's the right configuration for the pulse to make the direction interrupt stable?
            _Step.SetPulse(nanoseconds, nanoseconds - 10000 /*10 microseconds */);
            //start the runaway note timer
            _RunawayNoteTimer.Change(RUNAWAY_TIMEOUT, -1);
        }

        /// <summary>
        /// Stops any current note
        /// </summary>
        public void StopNote()
        {
            _Step.Set(true);
            //cancel the runaway note timer
            _RunawayNoteTimer.Change(-1, -1);
        }

        /// <summary>
        /// Calculates the frequence for a MIDI note (based on A440)
        /// </summary>
        double GetFrequencyForNote(int noteNumber)
        {
            int diff = noteNumber - 69; //69 - A
            return 440 * System.Math.Pow(2, (double)diff / 12);
        }
    }
}
