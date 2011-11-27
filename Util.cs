using System;
using Microsoft.SPOT;
using System.Diagnostics;

namespace GhostDrive
{
    static class Util
    {
        [Conditional("DEBUG")]
        public static void DebugPrint(string msg)
        {
            Debug.Print(msg);
        }
    }
}
