using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Infinity.Core.Udp
{
    public class UdpConnectionListener : NetworkConnectionListener
    {
        private const int send_receive_buffer_size = 1024 * 1024;

        private Socket socket;
        private ILogger logger;
        private Timer reliable_packet_timer;

        private ConcurrentDictionary<EndPoint, UdpServerConnection> all_connections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();

        public UdpListenerStatistics Statistics { get; private set; }

        public override double AveragePing => all_connections.Values.Sum(c => c.AveragePingMs) / all_connections.Count;
        public override int ConnectionCount => all_connections.Count;

        public UdpConnectionListener(IPEndPoint _endpoint, IPMode _ip_mode = IPMode.IPv4, ILogger _logger = null)
        {
            Statistics = new UdpListenerStatistics();

            logger = _logger;
            EndPoint = _endpoint;
            IPMode = _ip_mode;

            socket = CreateSocket(Protocol.Udp, IPMode);

            socket.ReceiveBufferSize = send_receive_buffer_size;
            socket.SendBufferSize = send_receive_buffer_size;

            reliable_packet_timer = new Timer(ManageReliablePackets, null, 100, Timeout.Infinite);
        }

        ~UdpConnectionListener()
        {
            Dispose(false);
        }

        private void ManageReliablePackets(object? _state)
        {
            foreach (var ep_connection in all_connections)
            {
                var connection = ep_connection.Value;
                connection.ManageReliablePackets();
            }

            try
            {
                reliable_packet_timer.Change(100, Timeout.Infinite);
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

        private void StartListeningForData()
        {
            EndPoint remoteEP = EndPoint;

            MessageReader reader = null;
            try
            {
                reader = MessageReader.Get();
                socket.BeginReceiveFrom(reader.Buffer, 0, reader.Buffer.Length, SocketFlags.None, ref remoteEP, ReadCallback, reader);
            }
            catch (SocketException sx)
            {
                reader?.Recycle();

                logger?.WriteError("Socket Ex in StartListening: " + sx.Message);

                Thread.Sleep(10);
                StartListeningForData();
                return;
            }
            catch (Exception ex)
            {
                reader?.Recycle();
                logger?.WriteError("Stopped due to: " + ex.Message);
                return;
            }
        }

        void ReadCallback(IAsyncResult _result)
        {
            MessageReader reader = (MessageReader)_result.AsyncState;
            int bytes_received;
            EndPoint remote_end_point = new IPEndPoint(EndPoint.Address, EndPoint.Port);

            //End the receive operation
            try
            {
                bytes_received = socket.EndReceiveFrom(_result, ref remote_end_point);

                reader.Offset = 0;
                reader.Length = bytes_received;
            }
            catch (ObjectDisposedException)
            {
                reader.Recycle();
                return;
            }
            catch (SocketException sx)
            {
                reader.Recycle();
                if (sx.SocketErrorCode == SocketError.NotConnected)
                {
                    InvokeInternalError(InfinityInternalErrors.ConnectionDisconnected);
                    return;
                }

                logger?.WriteError($"Socket Ex {sx.SocketErrorCode} in ReadCallback: {sx.Message}");

                Thread.Sleep(10);
                StartListeningForData();
                return;
            }
            catch (Exception ex)
            {
                // Idk, maybe a null ref after dispose?
                reader.Recycle();
                logger?.WriteError("Stopped due to: " + ex.Message);
                return;
            }

            // I'm a little concerned about a infinite loop here, but it seems like it's possible 
            // to get 0 bytes read on UDP without the socket being shut down.
            if (bytes_received == 0)
            {
                reader.Recycle();
                logger?.WriteInfo("Received 0 bytes");
                Thread.Sleep(10);
                StartListeningForData();
                return;
            }

            //Begin receiving again
            StartListeningForData();

            bool aware = true;
            bool is_handshake = reader.Buffer[0] == UdpSendOptionInternal.Handshake;

            // If we're aware of this connection use the one already
            // If this is a new client then connect with them!
            UdpServerConnection connection;
            if (!all_connections.TryGetValue(remote_end_point, out connection))
            {
                // Check for malformed connection attempts
                if (!is_handshake)
                {
                    reader.Recycle();
                    return;
                }

                if (HandshakeConnection != null && 
                    !HandshakeConnection((IPEndPoint)remote_end_point, reader.Buffer, out var response))
                {
                    reader.Recycle();
                    if (response != null)
                    {
                        SendData(response, response.Length, remote_end_point);
                    }

                    return;
                }

                aware = false;
                connection = new UdpServerConnection(this, (IPEndPoint)remote_end_point, IPMode, logger);
                all_connections.TryAdd(remote_end_point, connection);
            }

            // If it's a new connection invoke the NewConnection event.
            // This needs to happen before handling the message because in localhost scenarios, the ACK and
            // subsequent messages can happen before the NewConnection event sets up OnDataRecieved handlers
            if (!aware)
            {
                // Skip header and Handshake byte;
                reader.Offset = 3;
                reader.Length = bytes_received;
                reader.Position = 0;
                InvokeNewConnection(connection, reader);
            }

            // Inform the connection of the buffer (new connections need to send an ack back to client)
            connection.HandleReceive(reader, bytes_received);
        }

#if DEBUG
        public int TestDropRate = -1;
        private int drop_counter = 0;
#endif

        internal void SendData(byte[] _bytes, int _length, EndPoint _endpoint)
        {
            if (_length > _bytes.Length)
            {
                return;
            }

#if DEBUG
            if (TestDropRate > 0)
            {
                if (Interlocked.Increment(ref drop_counter) % TestDropRate == 0)
                {
                    return;
                }
            }
#endif

            try
            {
                socket.BeginSendTo(
                    _bytes,
                    0,
                    _length,
                    SocketFlags.None,
                    _endpoint,
                    SendCallback,
                    null);

                Statistics.AddBytesSent(_length);
            }
            catch (SocketException e)
            {
                logger?.WriteError("Could not send data as a SocketException occurred: " + e);
            }
            catch (ObjectDisposedException)
            {
                //Keep alive timer probably ran, ignore
                return;
            }
        }

        private void SendCallback(IAsyncResult _result)
        {
            try
            {
                socket.EndSendTo(_result);
            }
            catch { }
        }

        internal void SendDataSync(byte[] _bytes, int _length, EndPoint _endpoint)
        {
            try
            {
                socket.SendTo(
                    _bytes,
                    0,
                    _length,
                    SocketFlags.None,
                    _endpoint
                );

                Statistics.AddBytesSent(_length);
            }
            catch { }
        }

        internal void RemoveConnectionTo(EndPoint _endpoint)
        {
            all_connections.TryRemove(_endpoint, out var conn);
        }

        protected override void Dispose(bool _disposing)
        {
            foreach (var entry in all_connections)
            {
                entry.Value.Dispose();
            }

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
            try { socket.Dispose(); } catch { }

            reliable_packet_timer.Dispose();

            base.Dispose(_disposing);
        }
    }
}