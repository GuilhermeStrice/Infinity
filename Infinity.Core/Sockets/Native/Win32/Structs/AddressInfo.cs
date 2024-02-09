using System.Runtime.InteropServices;

namespace Infinity.Core.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    internal unsafe struct AddressInfo
    {
        public AddressInfoHints ai_flags;
        public AddressFamily ai_family;
        public SocketType ai_socktype;
        public ProtocolFamily ai_protocol;
        public ulong ai_addrlen;
        public sbyte* ai_canonname;
        public byte* ai_addr;
        public AddressInfo* ai_next;
    }
}
