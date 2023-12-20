using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Core
{
    public delegate bool HandshakeCheck(IPEndPoint endPoint, byte[] input, out byte[] response);

    public abstract class NetworkConnectionListener : IDisposable
    {
        public IPEndPoint ?EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        public int ReceiveBufferSize = 8096;

        public abstract double AveragePing { get; }
        public abstract int ConnectionCount { get; }

        public HandshakeCheck? HandshakeConnection;

        public event Action<NewConnectionEventArgs>? NewConnection;
        public event Action<InfinityInternalErrors>? OnInternalError;

        public abstract void Start();

        protected void InvokeNewConnection(NetworkConnection _connection, MessageReader _reader)
        {
            var args = new NewConnectionEventArgs(_connection, _reader);
            NewConnection?.Invoke(args);
        }

        protected void InvokeInternalError(InfinityInternalErrors _error)
        {
            OnInternalError?.Invoke(_error);
        }

        public Socket CreateSocket(Protocol _protocol, IPMode _ip_mode)
        {
            Socket socket;

            SocketType socket_type;
            ProtocolType protocol_type;

            if (_protocol == Protocol.Udp)
            {
                socket_type = SocketType.Dgram;
                protocol_type = ProtocolType.Udp;
            }
            else
            {
                socket_type = SocketType.Stream;
                protocol_type = ProtocolType.Tcp;
            }

            if (_ip_mode == IPMode.IPv4)
            {
                socket = new Socket(AddressFamily.InterNetwork, socket_type, protocol_type);
            }
            else
            {
                if (!Socket.OSSupportsIPv6)
                {
                    throw new InvalidOperationException("IPV6 not supported!");
                }

                socket = new Socket(AddressFamily.InterNetworkV6, socket_type, protocol_type);
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }

            if (_protocol == Protocol.Udp)
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, true);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    socket.IOControl(SIO_UDP_CONNRESET, new byte[1], null);
                }
            }
            else
            {
                socket.NoDelay = true;
            }

            return socket;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                NewConnection = null;
                OnInternalError = null;
            }
        }
    }
}
