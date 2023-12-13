using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Core
{
    /// <summary>
    ///     Abstract base class for a <see cref="Connection"/> to a remote end point via a network protocol like TCP or UDP.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    public abstract class NetworkConnection : IDisposable
    {
        /// <summary>
        ///     Called when a message has been received.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         DataReceived is invoked everytime a message is received from the end point of this connection, the message 
        ///         that was received can be found in the <see cref="DataReceivedEventArgs"/> alongside other information from the 
        ///         event.
        ///     </para>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Event_Thread_Safety_Warning']/*" />
        /// </remarks>
        /// <example>
        ///     <code language="C#" source="DocInclude/TcpClientExample.cs"/>
        /// </example>
        public event Action<DataReceivedEventArgs>? DataReceived;

        /// <summary>
        ///     Called when the end point disconnects or an error occurs.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Disconnected is invoked when the connection is closed due to an exception occuring or because the remote 
        ///         end point disconnected. If it was invoked due to an exception occuring then the exception is available 
        ///         in the <see cref="DisconnectedEventArgs"/> passed with the event.
        ///     </para>
        ///     <include file="DocInclude/common.xml" path="docs/item[@name='Event_Thread_Safety_Warning']/*" />
        /// </remarks>
        /// <example>
        ///     <code language="C#" source="DocInclude/TcpClientExample.cs"/>
        /// </example>
        public event Action<DisconnectedEventArgs>? Disconnected;

#if DEBUG
        public int TestLagMs = -1;
        public int TestDropRate = 0;
        protected int testDropCount = 0;
#endif

        public event EventHandler<MessageWriter>? BeforeSend;
        public event EventHandler<MessageReader>? BeforeReceive;

        /// <summary>
        ///     The remote end point of this Connection.
        /// </summary>
        /// <remarks>
        ///     This is the end point that this connection is connected to (i.e. the other device). This returns an abstract 
        ///     <see cref="ConnectionEndPoint"/> which can then be cast to an appropriate end point depending on the 
        ///     connection type.
        /// </remarks>
        public IPEndPoint? EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        /// <summary>
        ///     The state of this connection.
        /// </summary>
        /// <remarks>
        ///     All implementers should be aware that when this is set to ConnectionState.Connected it will
        ///     release all threads that are blocked on <see cref="WaitOnConnect"/>.
        /// </remarks>
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

        /// <summary>
        /// An event that gives us a chance to send well-formed disconnect messages to clients when an internal disconnect happens.
        /// </summary>
        public Func<InfinityInternalErrors, MessageWriter> ?OnInternalDisconnect;

        public virtual float AveragePingMs { get; }

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

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected abstract bool SendDisconnect(MessageWriter _writer);

        /// <summary>
        ///     Sends a number of bytes to the end point of the connection using the specified <see cref="SendOption"/>.
        /// </summary>
        /// <param name="msg">The message to send.</param>
        public abstract SendErrors Send(MessageWriter _writer);

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

        public abstract void Connect(byte[] _bytes = null, int _timeout = 5000);

        public abstract void ConnectAsync(byte[] _bytes = null);

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        protected void DisconnectRemote(string _reason, MessageReader _reader)
        {
            if (SendDisconnect(null))
            {
                InvokeDisconnected(_reason, _reader);
            }

            Dispose();
        }

        /// <summary>
        /// Called when socket is disconnected publicly
        /// </summary>
        public void DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
            var msg = OnInternalDisconnect?.Invoke(_error);
            Disconnect(_reason, msg);
        }

        /// <summary>
        ///     Called when the socket has been disconnected locally.
        /// </summary>
        public void Disconnect(string _reason, MessageWriter _writer = null)
        {
            if (SendDisconnect(_writer))
            {
                InvokeDisconnected(_reason, null);
            }

            Dispose();
        }

        /// <summary>
        ///     Invokes the DataReceived event.
        /// </summary>
        /// <param name="_reader">The bytes received.</param>
        /// <param name="sendOption">The <see cref="SendOption"/> the message was received with.</param>
        /// <remarks>
        ///     Invokes the <see cref="DataReceived"/> event on this connection to alert subscribers a new message has been
        ///     received. The bytes and the send option that the message was sent with should be passed in to give to the
        ///     subscribers.
        /// </remarks>
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

        /// <summary>
        ///     Invokes the Disconnected event.
        /// </summary>
        /// <param name="_reason">The exception, if any, that occurred to cause this.</param>
        /// <param name="_reader">Extra disconnect data</param>
        /// <remarks>
        ///     Invokes the <see cref="Disconnected"/> event to alert subscribres this connection has been disconnected either 
        ///     by the end point or because an error occurred. If an error occurred the error should be passed in in order to 
        ///     pass to the subscribers, otherwise null can be passed in.
        /// </remarks>
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

        /// <summary>
        ///     Disposes of this NetworkConnection.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Disposes of this NetworkConnection.
        /// </summary>
        /// <param name="disposing">Are we currently disposing?</param>
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
