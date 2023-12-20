using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Core
{
    public abstract class NetworkConnection : IDisposable
    {
        public event Action<DataReceivedEventArgs>? DataReceived;
        public event Action<DisconnectedEventArgs>? Disconnected;

        public event EventHandler<MessageWriter>? BeforeSend;
        public event EventHandler<MessageReader>? BeforeReceive;

#if DEBUG
        public int TestLagMs = -1;
        public int TestDropRate = 0;
        protected int testDropCount = 0;
#endif

        public IPEndPoint? EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        public Func<InfinityInternalErrors, MessageWriter>? OnInternalDisconnect;

        public virtual float AveragePingMs { get; }

        public ConnectionState State
        {
            get
            {
                return state;
            }

            protected set
            {
                state = value;
                SetState(value);
            }
        }

        protected ConnectionState state;

        protected abstract void SetState(ConnectionState _state);

        protected NetworkConnection()
        {
            State = ConnectionState.NotConnected;
        }

        public long GetIP4Address()
        {
            if (IPMode == IPMode.IPv4)
            {
                return EndPoint.Address.Address;
            }
            else
            {
                var bytes = EndPoint.Address.GetAddressBytes();
                return BitConverter.ToInt64(bytes, bytes.Length - 8);
            }
        }

        protected abstract bool SendDisconnect(MessageWriter _writer);

        public abstract SendErrors Send(MessageWriter _writer);

        protected Socket CreateSocket(Protocol _protocol, IPMode _ip_mode)
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

        public abstract void Connect(MessageWriter _writer, int _timeout = 5000);

        public abstract void ConnectAsync(MessageWriter _writer);

        protected void DisconnectRemote(string _reason, MessageReader _reader)
        {
            if (SendDisconnect(null))
            {
                InvokeDisconnected(_reason, _reader);
            }

            Dispose();
        }

        protected void DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
            var msg = OnInternalDisconnect?.Invoke(_error);
            Disconnect(_reason, msg);
        }

        public void Disconnect(string _reason, MessageWriter _writer = null)
        {
            if (SendDisconnect(_writer))
            {
                InvokeDisconnected(_reason, null);
            }

            Dispose();
        }

        protected void InvokeDataReceived(MessageReader _reader)
        {
            if (DataReceived != null)
            {
                var args = new DataReceivedEventArgs(this, _reader);
                DataReceived.Invoke(args);
            }
            else
            {
                _reader.Recycle();
            }
        }

        protected void InvokeDisconnected(string _reason, MessageReader _reader)
        {
            if (Disconnected != null)
            {
                var args = new DisconnectedEventArgs(this, _reason, _reader);
                Disconnected.Invoke(args);
            }
            else
            {
                if (_reader != null)
                {
                    _reader.Recycle();
                }
            }
        }

        protected void InvokeBeforeSend(MessageWriter _writer)
        {
            BeforeSend?.Invoke(this, _writer);
        }

        protected void InvokeBeforeReceive(MessageReader _reader)
        {
            BeforeReceive?.Invoke(this, _reader);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                DataReceived = null;
                Disconnected = null;
            }
        }
    }
}
