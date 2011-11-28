using System;
using Microsoft.SPOT;
using System.IO.Ports;
using System.Threading;

namespace GhostSlave
{
    /// <summary>
    /// Handy extensions
    /// </summary>
    static class Extensions
    {
        /// <summary>
        /// Waits for <paramref name="count"/> bytes to be available for reading
        /// by polling for data every 10ms,
        /// and then reads that many bytes into <paramref name="buffer"/>
        /// at <paramref name="offset"/>
        /// </summary>
        /// <returns>The number of bytes read (should always match <paramref name="count"/>)</returns>
        static public int GuaranteedRead(this SerialPort port, byte[] buffer, int offset, int count)
        {
            return GuaranteedRead(port, buffer, offset, count, 0);
        }

        /// <summary>
        /// Waits for <paramref name="count"/> bytes to be available for reading,
        /// and then reads that many bytes into <paramref name="buffer"/>
        /// at <paramref name="offset"/>.  This overload allows you to specify the interval
        /// at which to poll for new bytes.
        /// </summary>
        /// <returns>The number of bytes read (should always match <paramref name="count"/>)</returns>
        static public int GuaranteedRead(this SerialPort port, byte[] buffer, int offset, int count, int pollInterval)
        {
            while (port.BytesToRead < count)
            {
                Thread.Sleep(pollInterval);
            }
            return port.Read(buffer, offset, count);
        }
    }
}

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Extension attribute to enable extension methods
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
    sealed class ExtensionAttribute : Attribute
    {
    }
}
