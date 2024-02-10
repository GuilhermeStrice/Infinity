using Infinity.Core.Net.Sockets;
using Infinity.Core.Net.Sockets.Native;
using Infinity.Core.Net.Sockets.Native.Win32;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Infinity.Core.Net
{
    public class Util
    {
        public static nint getaddrinfo(AddressFamily _address_family)
        {
            var hints = new AddressInfo();
            if (_address_family == AddressFamily.InterNetwork)
            {
                hints.ai_family = (int)AddressFamily.InterNetwork;
                hints.ai_socktype = (int)SocketType.Dgram;
                hints.ai_flags = (int)AddressInfoHints.AI_PASSIVE;
            }
            else
            {
                hints.ai_family = (int)AddressFamily.InterNetworkV6;
                hints.ai_socktype = (int)SocketType.Dgram;
                hints.ai_flags = (int)AddressInfoHints.AI_PASSIVE;
            }

            var result = new AddressInfo();

            var res_ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf(result));
            Marshal.StructureToPtr(result, res_ptr, false);

            if (Platform.IsWindows)
            {
                Winsock2.getaddrinfo("..localmachine", null, ref hints, ref res_ptr);
            }
            else
            {
                // linux impl
            }

            return res_ptr;
        }

        public static void freeaddrinfo(nint _address_info)
        {
            if (Platform.IsWindows)
            {
                Winsock2.freeaddrinfo(_address_info);
            }
            else
            {
                // linux impl
            }
        }
    }
}
