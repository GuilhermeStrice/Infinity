using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Infinity.Core.TickManager
{
    internal static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        [ResourceExposure(ResourceScope.None)]
        public static extern bool QueryPerformanceCounter(out long value);
    }

    /// <summary>
    /// Helper functions
    /// </summary>
    public static class TickHelper
    {
        /// <summary>
        /// Convert ticks per second to milliseconds per tick
        /// </summary>
        /// <param name="tps">Ticks per second</param>
        /// <returns>Milliseconds per tick</returns>
        public static double TpsToMspt(double tps)
        {
            return 1D / tps * 1000;
        }

        /// <summary>
        /// Converts milliseconds per tick to ticks per second
        /// </summary>
        /// <param name="mspt">Milliseconds per tick</param>
        /// <returns>Ticks per second</returns>
        public static double MsptToTps(double mspt)
        {
            return 1D / mspt * 1000;
        }

        internal static long GetCurrentTimestamp()
        {
            long timestamp;

            // took it from Stopwatch
            // maybe find a better way to get the current timestamp
            NativeMethods.QueryPerformanceCounter(out timestamp);
            return timestamp;
        }
    }
}
