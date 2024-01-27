using System.Net;
using System.Net.Sockets;

namespace Infinity.Core.Tcp
{
    /// <summary>
    ///     Listens for new TCP connections and creates TCPConnections for them.
    /// </summary>
    public sealed class TcpConnectionListener : NetworkConnectionListener
    {
        /// <summary>
        ///     The socket listening for connections.
        /// </summary>
        Socket listener;

        public override double AveragePing => throw new NotImplementedException();

        public override int ConnectionCount => throw new NotImplementedException();

        /// <summary>
        ///     Creates a new TcpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="endPoint">The end point to listen on.</param>
        public TcpConnectionListener(IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4, ILogger logger = null)
        {
            EndPoint = endPoint;
            IPMode = ipMode;

            if (IPMode == IPMode.IPv4)
                listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new InfinityException("IPV6 not supported!");

                listener = new Socket(AddressFamily.InterNetworkV6, SocketType.Stream, ProtocolType.Tcp);
                listener.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }

            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        }

        public override void Start()
        {
            try
            {
                listener.Bind(EndPoint);
                listener.Listen(1000);

                listener.BeginAccept(acceptConnection, null);
            }
            catch (SocketException e)
            {
                throw new InfinityException("Could not start listening as a SocketException occured", e);
            }
        }

        /// <summary>
        ///     Called when a new connection has been accepted by the listener.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void acceptConnection(IAsyncResult result)
        {
            //Accept Tcp socket
            Socket tcpSocket;
            try
            {
                tcpSocket = listener.EndAccept(result);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
                return;
            }
            catch (SocketException)
            {
                // probably closed
                return;
            }

            //Start listening for the next connection
            listener.BeginAccept(new AsyncCallback(acceptConnection), null);

            //Sort the event out
            TcpConnection tcpConnection = new TcpConnection(tcpSocket);
            tcpConnection.OnHandshake += TcpConnection_OnHandshake;

            tcpConnection.StartReceiving();
        }

        private void TcpConnection_OnHandshake(MessageReader handshakeData, TcpConnection connection)
        {
            if (HandshakeConnection != null)
            {
                if (!HandshakeConnection(connection.EndPoint, handshakeData, out var response))
                {
                    handshakeData.Recycle();
                    if (response != null)
                    {
                        connection.SendBytes(response);
                    }

                    return;
                }
            }

            InvokeNewConnection(connection, handshakeData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (listener)
                    listener.Close();
            }

            base.Dispose(disposing);
        }
    }
}