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
    class FloppySynth
    {
        PWM _Step;
        //TODO: remove PWM requirement?
        PWM _Dir;
        InterruptPort _Interrupt;
        OutputPort _Disable;
        byte _TrackLocation;

        /// <summary>
        /// Creates a floppy synth instance
        /// </summary>
        /// <param name="disablePin">The pin connected to the /enable signal</param>
        /// <param name="stepPin">The PWM-capable pin connected to the /step signal</param>
        /// <param name="interruptPin">The interrupt-capable IO pin connected to the /step signal (to drive the dir pin)</param>
        /// <param name="dirPin">The PWM-capable pin connected to the dir signal</param>
        /// <param name="trackLocation"></param>
        public FloppySynth(FEZ_Pin.Digital disablePin, PWM.Pin stepPin, FEZ_Pin.Digital interruptPin, PWM.Pin dirPin, byte trackLocation)
        {
            var dirState = false;
            _Interrupt = new InterruptPort((Cpu.Pin)interruptPin, false, Port.ResistorMode.Disabled, Port.InterruptMode.InterruptEdgeLow);
            _Step = new PWM(stepPin);
            _Dir = new PWM(dirPin);
            //when the interrupt is triggered, toggle the direction
            _Interrupt.OnInterrupt += (d1, d2, t) =>
            {
                dirState = !dirState;
                _Dir.Set(dirState);
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
            _Dir.Set(false);
            for (int i = 0; i < 90; i++)
            {
                _Step.Set(true);
                Thread.Sleep(10);
                _Step.Set(false);
                Thread.Sleep(1);
            }
            _Dir.Set(true);
            for (int i = 0; i < 90; i++)
            {
                _Step.Set(true);
                Thread.Sleep(10);
                _Step.Set(false);
                Thread.Sleep(1);
            }
            _Dir.Set(false);
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
            //TODO: make octave lowering an option (currently lowering it by one octave to avoid stepping too fast)
            var frequency = GetFrequencyForNote(note) / 2;
            var period = 1.0/frequency; //seconds
            var nanoseconds = (uint)(period * 1000000000);
            //TODO: what's the right configuration for the pulse to make the direction interrupt stable?
            _Step.SetPulse(nanoseconds, nanoseconds - 10000 /*10 microseconds */);
        }

        /// <summary>
        /// Stops any current note
        /// </summary>
        public void StopNote()
        {
            _Step.Set(true);
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
