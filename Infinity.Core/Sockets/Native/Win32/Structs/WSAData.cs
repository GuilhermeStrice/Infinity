using System.Runtime.InteropServices;

namespace Infinity.Core.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct WSAData
    {
        internal short wVersion;
        internal short wHighVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
        internal string szDescription;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
        internal string szSystemStatus;

        internal short iMaxSockets;
        internal short iMaxUdpDg;
        internal IntPtr lpVendorInfo;
    }
}
