using System.Runtime.InteropServices;

namespace Infinity.Core.Net.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SocketAddressInput
    {
        public ushort sin_family;
        public ushort sin_port;
        public InputAddressUnion sin_addr;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
        public string sin_zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputAddressB
    {
        public byte s_b1;
        public byte s_b2;
        public byte s_b3;
        public byte s_b4;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InputAddressW
    {
        public ushort s_w1;
        public ushort s_w2;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputAddressUnion
    {
        [FieldOffset(0)]
        public InputAddressB S_un_b;

        [FieldOffset(0)]
        public InputAddressW S_un_w;

        [FieldOffset(0)]
        public uint S_addr;
    }
}
