using System.Runtime.InteropServices;
using System.Text;

namespace Infinity.Core.Net.Sockets.Native.Win32
{
    internal static unsafe class Winsock2
    {
        [DllImport("Ws2_32.dll", CharSet = CharSet.Ansi)]
        internal static extern int getaddrinfo(
            [In] [MarshalAs(UnmanagedType.LPStr)] string pNodeName,
            [In] [MarshalAs(UnmanagedType.LPStr)] string pServiceName, 
            [In] ref AddressInfo pHints,
            [In, Out] ref nint ppResult);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern void freeaddrinfo([In] IntPtr info);

        [DllImport("Ws2_32.dll", CharSet = CharSet.Unicode, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern SocketError GetNameInfoW(
            [In] byte[] sa,
            [In] int salen,
            [Out] StringBuilder host,
            [In] int hostlen,
            [Out] StringBuilder serv,
            [In] int servlen,
            [In] int flags);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern nint accept(
            [In] nint socketHandle,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError bind(
            [In] nint socketHandle,
            [In] byte[] socketAddress,
            [In] int socketAddressSize);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError closesocket([In] nint socketHandle);

        [DllImport("Ws2_32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern SocketError gethostname(
                                            [Out] StringBuilder hostName,
                                            [In] int bufferLength
                                            );

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getpeername(
            [In] nint socketHandle,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockname(
            [In] nint socketHandle,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out int optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] byte[] optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out Linger optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out IPMulticastRequest optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out IPv6MulticastRequest optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError ioctlsocket(
            [In] nint handle,
            [In] int cmd,
            [In, Out] ref int argp);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError listen(
            [In] nint socketHandle,
            [In] int backlog);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int recv(
            [In] nint socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int recvfrom(
            [In] nint socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int select(
            [In] int ignoredParameter,
            [In, Out] nint[] readfds,
            [In, Out] nint[] writefds,
            [In, Out] nint[] exceptfds,
            [In] ref TimeValue timeout);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int select(
            [In] int ignoredParameter,
            [In, Out] nint[] readfds,
            [In, Out] nint[] writefds,
            [In, Out] nint[] exceptfds,
            [In] nint nullTimeout);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int send(
            [In] nint socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int sendto(
            [In] nint socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [In] byte[] socketAddress,
            [In] int socketAddressSize);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] nint handle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref Linger linger,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref int optionValue,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] byte[] optionValue,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref nint pointer,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref IPMulticastRequest mreq,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] nint socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref IPv6MulticastRequest mreq,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError shutdown(
            [In] nint socketHandle,
            [In] int how);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern nint socket(
            AddressFamily _address_family,
            SocketType _socket_type,
            ProtocolType _protocol_type);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern int connect(
            [In] nint _socket,
            [In] ref byte[] _address,
            [In] int _address_length);

        [DllImport("Ws2_32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern SocketError WSAStartup(
                                           [In] short wVersionRequested,
                                           [Out] out WSAData lpWSAData
                                           );

        [DllImport("Ws2_32.dll")]
        public static extern SocketError WSAGetLastError();
    }
}
