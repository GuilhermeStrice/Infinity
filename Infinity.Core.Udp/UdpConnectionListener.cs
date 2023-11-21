using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Listens for new UDP connections and creates UdpConnections for them.
    /// </summary>
    public class UdpConnectionListener : NetworkConnectionListener
    {
        private const int SendReceiveBufferSize = 1024 * 1024;

        private Socket socket;
        private ILogger Logger;
        private Timer reliablePacketTimer;

        private ConcurrentDictionary<EndPoint, UdpServerConnection> allConnections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();

        public UdpListenerStatistics Statistics { get; private set; }

        public override double AveragePing => allConnections.Values.Sum(c => c.AveragePingMs) / allConnections.Count;
        public override int ConnectionCount { get { return allConnections.Count; } }

        /// <summary>
        ///     Creates a new UdpConnectionListener for the given <see cref="IPAddress"/>, port and <see cref="IPMode"/>.
        /// </summary>
        /// <param name="endPoint">The endpoint to listen on.</param>
        public UdpConnectionListener(IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4, ILogger logger = null)
        {
            Statistics = new UdpListenerStatistics();

            Logger = logger;
            EndPoint = endPoint;
            IPMode = ipMode;

            socket = CreateSocket(IPMode);

            socket.ReceiveBufferSize = SendReceiveBufferSize;
            socket.SendBufferSize = SendReceiveBufferSize;

            reliablePacketTimer = new Timer(ManageReliablePackets, null, 100, Timeout.Infinite);
        }

        ~UdpConnectionListener()
        {
            Dispose(false);
        }

        public Socket CreateSocket(IPMode ipMode)
        {
            Socket socket;
            if (ipMode == IPMode.IPv4)
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            else
            {
                if (!Socket.OSSupportsIPv6)
                    throw new InvalidOperationException("IPV6 not supported!");

                socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, false);
            }

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            try
            {
                socket.DontFragment = false;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    socket.IOControl(SIO_UDP_CONNRESET, new byte[1], null);
                }
            }
            catch { }

            return socket;
        }

        private void ManageReliablePackets(object ?state)
        {
            foreach (var kvp in allConnections)
            {
                var sock = kvp.Value;
                sock.ManageReliablePackets();
            }

            try
            {
                reliablePacketTimer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        public override void Start()
        {
            try
            {
                socket.Bind(EndPoint);
            }
            catch (SocketException e)
            {
                throw new InfinityException("Could not start listening as a SocketException occurred", e);
            }

            StartListeningForData();
        }

        /// <summary>
        ///     Instructs the listener to begin listening.
        /// </summary>
        private void StartListeningForData()
        {
            EndPoint remoteEP = EndPoint;

            MessageReader message = null;
            try
            {
                message = MessageReader.GetSized(ReceiveBufferSize);
                socket.BeginReceiveFrom(message.Buffer, 0, message.Buffer.Length, SocketFlags.None, ref remoteEP, ReadCallback, message);
            }
            catch (SocketException sx)
            {
                message?.Recycle();

                Logger?.WriteError("Socket Ex in StartListening: " + sx.Message);

                Thread.Sleep(10);
                StartListeningForData();
                return;
            }
            catch (Exception ex)
            {
                message?.Recycle();
                Logger?.WriteError("Stopped due to: " + ex.Message);
                return;
            }
        }

        void ReadCallback(IAsyncResult result)
        {
            var message = (MessageReader)result.AsyncState;
            int bytesReceived;
            EndPoint remoteEndPoint = new IPEndPoint(EndPoint.Address, EndPoint.Port);

            //End the receive operation
            try
            {
                bytesReceived = socket.EndReceiveFrom(result, ref remoteEndPoint);

                message.Offset = 0;
                message.Length = bytesReceived;
            }
            catch (ObjectDisposedException)
            {
                message.Recycle();
                return;
            }
            catch (SocketException sx)
            {
                message.Recycle();
                if (sx.SocketErrorCode == SocketError.NotConnected)
                {
                    InvokeInternalError(InfinityInternalErrors.ConnectionDisconnected);
                    return;
                }

                // Client no longer reachable, pretend it didn't happen
                // TODO should this not inform the connection this client is lost???

                // This thread suggests the IP is not passed out from WinSoc so maybe not possible
                // http://stackoverflow.com/questions/2576926/python-socket-error-on-udp-data-receive-10054
                Logger?.WriteError($"Socket Ex {sx.SocketErrorCode} in ReadCallback: {sx.Message}");

                Thread.Sleep(10);
                StartListeningForData();
                return;
            }
            catch (Exception ex)
            {
                // Idk, maybe a null ref after dispose?
                message.Recycle();
                Logger?.WriteError("Stopped due to: " + ex.Message);
                return;
            }

            // I'm a little concerned about a infinite loop here, but it seems like it's possible 
            // to get 0 bytes read on UDP without the socket being shut down.
            if (bytesReceived == 0)
            {
                message.Recycle();
                Logger?.WriteInfo("Received 0 bytes");
                Thread.Sleep(10);
                StartListeningForData();
                return;
            }

            //Begin receiving again
            StartListeningForData();

            bool aware = true;
            bool isHandshake = message.Buffer[0] == UdpSendOptionInternal.Handshake;

            // If we're aware of this connection use the one already
            // If this is a new client then connect with them!
            UdpServerConnection connection;
            if (!allConnections.TryGetValue(remoteEndPoint, out connection))
            {
                // Check for malformed connection attempts
                if (!isHandshake)
                {
                    message.Recycle();
                    return;
                }

                if (HandshakeConnection != null)
                {
                    if (!HandshakeConnection((IPEndPoint)remoteEndPoint, message.Buffer, out var response))
                    {
                        message.Recycle();
                        if (response != null)
                        {
                            SendData(response, response.Length, remoteEndPoint);
                        }

                        return;
                    }
                }

                aware = false;
                connection = new UdpServerConnection(this, (IPEndPoint)remoteEndPoint, IPMode, Logger);
                allConnections.TryAdd(remoteEndPoint, connection);
            }

            // If it's a new connection invoke the NewConnection event.
            // This needs to happen before handling the message because in localhost scenarios, the ACK and
            // subsequent messages can happen before the NewConnection event sets up OnDataRecieved handlers
            if (!aware)
            {
                // Skip header and Handshake byte;
                message.Offset = 4;
                message.Length = bytesReceived - 4;
                message.Position = 0;
                InvokeNewConnection(message, connection);
            }

            // Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(message, bytesReceived);
        }

#if DEBUG
        public int TestDropRate = -1;
        private int dropCounter = 0;
#endif

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendData(byte[] bytes, int length, EndPoint endPoint)
        {
            if (length > bytes.Length) return;

#if DEBUG
            if (TestDropRate > 0)
            {
                if (Interlocked.Increment(ref dropCounter) % TestDropRate == 0)
                {
                    return;
                }
            }
#endif

            try
            {
                socket.BeginSendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint,
                    SendCallback,
                    null);

                Statistics.AddBytesSent(length);
            }
            catch (SocketException e)
            {
                Logger?.WriteError("Could not send data as a SocketException occurred: " + e);
            }
            catch (ObjectDisposedException)
            {
                //Keep alive timer probably ran, ignore
                return;
            }
        }

        private void SendCallback(IAsyncResult result)
        {
            try
            {
                socket.EndSendTo(result);
            }
            catch { }
        }

        /// <summary>
        ///     Sends data from the listener socket.
        /// </summary>
        /// <param name="bytes">The bytes to send.</param>
        /// <param name="endPoint">The endpoint to send to.</param>
        internal void SendDataSync(byte[] bytes, int length, EndPoint endPoint)
        {
            try
            {
                socket.SendTo(
                    bytes,
                    0,
                    length,
                    SocketFlags.None,
                    endPoint
                );

                Statistics.AddBytesSent(length);
            }
            catch { }
        }

        /// <summary>
        ///     Removes a virtual connection from the list.
        /// </summary>
        /// <param name="endPoint">The endpoint of the virtual connection.</param>
        internal void RemoveConnectionTo(EndPoint endPoint)
        {
            allConnections.TryRemove(endPoint, out var conn);
        }

        protected override void Dispose(bool disposing)
        {
            foreach (var kvp in allConnections)
            {
                kvp.Value.Dispose();
            }

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
            try { socket.Dispose(); } catch { }

            reliablePacketTimer.Dispose();

            base.Dispose(disposing);
        }
    }
}