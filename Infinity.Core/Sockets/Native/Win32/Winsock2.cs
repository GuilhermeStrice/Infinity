using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace Infinity.Core.Sockets.Native.Win32
{
    internal static unsafe class Winsock2
    {
        [DllImport("Ws2_32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern int GetAddrInfoW(
            [In] string nodename,
            [In] string servicename,
            [In] ref AddressInfo hints,
            [Out] out IntPtr handle
            );

        internal const string GetAddrInfoExCancelFunctionName = "GetAddrInfoExCancel";

        internal delegate void LPLOOKUPSERVICE_COMPLETION_ROUTINE([In] int dwError, [In] int dwBytes, [In] NativeOverlapped* lpOverlapped);

        [DllImport("Ws2_32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern int GetAddrInfoExW(
            [In] string pName,
            [In] string pServiceName,
            [In] int dwNamespace,
            [In] IntPtr lpNspId,
            [In] ref AddressInfoEx pHints,
            [Out] out AddressInfoEx* ppResult,
            [In] IntPtr timeout,
            [In] ref NativeOverlapped lpOverlapped,
            [In] LPLOOKUPSERVICE_COMPLETION_ROUTINE lpCompletionRoutine,
            [Out] out IntPtr lpNameHandle
        );

        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern void FreeAddrInfoEx([In] AddressInfoEx* pAddrInfo);

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
        internal static extern IntPtr accept(
            [In] IntPtr socketHandle,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError bind(
            [In] IntPtr socketHandle,
            [In] byte[] socketAddress,
            [In] int socketAddressSize);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError closesocket([In] IntPtr socketHandle);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern void freeaddrinfo([In] IntPtr info);

        [DllImport("Ws2_32.dll", CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        internal static extern SocketError gethostname(
                                            [Out] StringBuilder hostName,
                                            [In] int bufferLength
                                            );

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getpeername(
            [In] IntPtr socketHandle,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockname(
            [In] IntPtr socketHandle,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out int optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] byte[] optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out Linger optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out IPMulticastRequest optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [Out] out IPv6MulticastRequest optionValue,
            [In, Out] ref int optionLength);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError ioctlsocket(
            [In] IntPtr handle,
            [In] int cmd,
            [In, Out] ref int argp);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError listen(
            [In] IntPtr socketHandle,
            [In] int backlog);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int recv(
            [In] IntPtr socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int recvfrom(
            [In] IntPtr socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [Out] byte[] socketAddress,
            [In, Out] ref int socketAddressSize);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int select(
            [In] int ignoredParameter,
            [In, Out] IntPtr[] readfds,
            [In, Out] IntPtr[] writefds,
            [In, Out] IntPtr[] exceptfds,
            [In] ref TimeValue timeout);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int select(
            [In] int ignoredParameter,
            [In, Out] IntPtr[] readfds,
            [In, Out] IntPtr[] writefds,
            [In, Out] IntPtr[] exceptfds,
            [In] IntPtr nullTimeout);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int send(
            [In] IntPtr socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern int sendto(
            [In] IntPtr socketHandle,
            [In] byte* pinnedBuffer,
            [In] int len,
            [In] SocketFlags socketFlags,
            [In] byte[] socketAddress,
            [In] int socketAddressSize);

        [DllImport("Ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] IntPtr handle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref Linger linger,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref int optionValue,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] byte[] optionValue,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref IntPtr pointer,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref IPMulticastRequest mreq,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt(
            [In] IntPtr socketHandle,
            [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName,
            [In] ref IPv6MulticastRequest mreq,
            [In] int optionLength);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        internal static extern SocketError shutdown(
            [In] IntPtr socketHandle,
            [In] int how);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern IntPtr socket(
            AddressFamily _address_family,
            SocketType _socket_type,
            ProtocolType _protocol_type);

        [DllImport("Ws2_32.dll", SetLastError = true)]
        public static extern int connect(
            [In] IntPtr _socket,
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
