using System.Runtime.InteropServices;

namespace Infinity.Core.Net.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct Linger
    {
        internal ushort OnOff;
        internal ushort Time;
    }
}
