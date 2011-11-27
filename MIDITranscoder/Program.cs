using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MIDITranscoder
{
    /// <summary>
    /// This app transcodes MIDI files into a form suitable for playing on the devices.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            //TODO: arg checks
            var source = args[0];
            var dest = args[1];
        }
    }
}
