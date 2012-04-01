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
using System.Collections;

namespace GhostDrive
{
    public class Program {
        static IFloppySynth _LocalSynth = new FloppySynth(FEZ_Pin.Digital.IO43, PWM.Pin.PWM4, FEZ_Pin.Digital.IO41, FEZ_Pin.Digital.IO2, 1);

        public static void Main() {
            // Setup board LED
            OutputPort led = new OutputPort((Cpu.Pin)FEZ_Pin.Digital.LED, false);

            var remoteManager = new RemoteManager(new SerialPort("COM4", 115200, Parity.None, 8, StopBits.One));
            remoteManager.OnNoteOn += note => { _LocalSynth.PlayNote(note); led.Write(true); };
            remoteManager.OnNoteOff += () => { _LocalSynth.StopNote(); led.Write(false); };
            remoteManager.OnEnable += _LocalSynth.Enable;
            remoteManager.OnDisable += _LocalSynth.Disable;
            remoteManager.OnSetModulation += mod => _LocalSynth.OctaveModulation = mod;

            remoteManager.XBee.WriteAtCommand("ID", new byte[] { 0x02, 0x34 });
            Thread.Sleep(500);
            remoteManager.XBee.WriteAtCommand("NR", new byte[] { 0 });

            Thread.Sleep(Timeout.Infinite);
        }
    }
}
