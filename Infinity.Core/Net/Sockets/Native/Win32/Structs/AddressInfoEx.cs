using System.Runtime.InteropServices;

namespace Infinity.Core.Net.Sockets.Native.Win32
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct AddressInfoEx
    {
        internal AddressInfoHints ai_flags;
        internal AddressFamily ai_family;
        internal SocketType ai_socktype;
        internal ProtocolFamily ai_protocol;
        internal int ai_addrlen;
        internal nint ai_canonname;
        internal byte* ai_addr;
        internal nint ai_blob;
        internal int ai_bloblen;
        internal nint ai_provider;
        internal AddressInfoEx* ai_next;
    }
}
