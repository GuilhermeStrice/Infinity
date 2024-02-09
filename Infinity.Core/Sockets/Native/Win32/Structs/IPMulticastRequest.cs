using System.Runtime.InteropServices;

namespace Infinity.Core.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IPMulticastRequest
    {
        internal int MulticastAddress;
        internal int InterfaceAddress;

        internal static readonly int Size = Marshal.SizeOf<IPMulticastRequest>();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IPv6MulticastRequest
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        internal byte[] MulticastAddress;
        internal int InterfaceIndex;

        internal static readonly int Size = Marshal.SizeOf<IPv6MulticastRequest>();
    }
}
