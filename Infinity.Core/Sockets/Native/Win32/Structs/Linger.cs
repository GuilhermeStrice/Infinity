using System.Runtime.InteropServices;

namespace Infinity.Core.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Linger
    {
        internal ushort OnOff; // Option on/off.
        internal ushort Time; // Linger time in seconds.
    }
}
