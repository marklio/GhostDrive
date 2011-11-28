using System;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;

using GHIElectronics.NETMF.FEZ;
using System.IO.Ports;
using GhostDrive;
using GHIElectronics.NETMF.Hardware;

namespace GhostSlave
{
    public class Program
    {
        static SerialPort _Port = new SerialPort("COM3", 115200, Parity.None, 8, StopBits.One);
        static IFloppySynth _LocalSynth = new FloppySynth(FEZ_Pin.Digital.Di5, PWM.Pin.PWM1, FEZ_Pin.Digital.Di4, FEZ_Pin.Digital.Di2, 1);
        public static void Main()
        {
            // Blink board LED

            bool ledState = false;

            OutputPort led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, ledState);

            byte[] readBuffer = new byte[3];
            _Port.Open();
            while (true)
            {
                _Port.GuaranteedRead(readBuffer, 0, 3);
                //if we didn't land on the magic number, read a single byte and continue.  Hopefully, we'll get back on.
                //TODO: is this sufficient here?
                if (readBuffer[0] != 0x65)
                {
                    _Port.GuaranteedRead(readBuffer, 0, 1);
                    continue;
                }
                switch ((FrameType)readBuffer[1])
                {
                    case FrameType.Enable:
                        _LocalSynth.Enable();
                        break;
                    case FrameType.Disable:
                        _LocalSynth.Disable();
                        break;
                    case FrameType.NoteOff:
                        _LocalSynth.StopNote();
                        led.Write(false);
                        break;
                    case FrameType.NoteOn:
                        _LocalSynth.PlayNote((int)readBuffer[2]);
                        led.Write(true);
                        break;
                    case FrameType.ResetLocation:
                        _LocalSynth.ResetLocation();
                        break;
                    case FrameType.SetLocation:
                        _LocalSynth.SetLocation(readBuffer[2]);
                        break;
                    case FrameType.SetModulation:
                        var value = (sbyte)readBuffer[2];
                        _LocalSynth.OctaveModulation = value;
                        break;
                }
            }
        }

    }
}
