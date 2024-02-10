using System.Runtime.InteropServices;

namespace Infinity.Core.Net.Sockets.Native
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct AddressInfo
    {
        public int ai_flags;
        public int ai_family;
        public int ai_socktype;
        public int ai_protocol;
        public ulong ai_addrlen;

        [MarshalAs(UnmanagedType.LPStr)]
        public string ai_canonname;

        public nint ai_addr;
        public nint ai_next;
    }
}
