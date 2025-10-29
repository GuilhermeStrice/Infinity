using Infinity.Core;
using Infinity.Core.Exceptions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Udp
{
    public class UdpConnectionListener : NetworkConnectionListener
    {
        public UdpConnectionConfiguration Configuration { get; set; } = new UdpConnectionConfiguration(); // default config

        public UdpListenerStatistics Statistics { get; private set; }

        public override double AveragePing => all_connections.Values.Sum(c => c.AveragePingMs) / all_connections.Count;
        public override int ConnectionCount => all_connections.Count;

        private const int send_receive_buffer_size = 1024 * 1024;

        private Socket socket;
        private ILogger logger;
        private Timer reliable_packet_timer;

        private CancellationTokenSource cancellation_token_source = new CancellationTokenSource();

        private ConcurrentDictionary<EndPoint, UdpServerConnection> all_connections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();

        public UdpConnectionListener(IPEndPoint _endpoint, IPMode _ip_mode = IPMode.IPv4, ILogger _logger = null)
        {
            Statistics = new UdpListenerStatistics();

            logger = _logger;
            EndPoint = _endpoint;
            IPMode = _ip_mode;

            socket = CreateSocket(IPMode);

            socket.ReceiveBufferSize = send_receive_buffer_size;
            socket.SendBufferSize = send_receive_buffer_size;

            reliable_packet_timer = new Timer(ManageReliablePackets, null, 100, Timeout.Infinite);
        }

        protected Socket CreateSocket(IPMode _ip_mode)
        {
            Socket socket;

            SocketType socket_type;
            ProtocolType protocol_type;

            socket_type = SocketType.Dgram;
            protocol_type = ProtocolType.Udp;

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

            socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                const int SIO_UDP_CONNRESET = -1744830452;
                socket.IOControl(SIO_UDP_CONNRESET, new byte[1], null);
            }

            return socket;
        }

        ~UdpConnectionListener()
        {
            cancellation_token_source.Cancel();
            Thread.Sleep(500);
            Dispose(false);
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

            _ = Task.Run(StartListeningForData);
        }

        private async Task StartListeningForData()
        {
            while (!cancellation_token_source.IsCancellationRequested)
            {
                EndPoint remoteEP = EndPoint;

                MessageReader reader = MessageReader.Get();

                try
                {
                    var result = await socket.ReceiveFromAsync(reader.Buffer, SocketFlags.None, remoteEP);
                    reader.Length = result.ReceivedBytes;
                    reader.Position = 0;

                    await ReadCallback(reader, result.RemoteEndPoint);
                }
                catch (SocketException sx)
                {
                    reader.Recycle();

                    logger?.WriteError($"Socket Ex {sx.SocketErrorCode} in ReadCallback: {sx.Message}");

                    Thread.Sleep(10);
                    return;
                }
                catch (Exception ex)
                {
                    reader.Recycle();
                    logger?.WriteError("Stopped due to: " + ex.Message);
                    return;
                }
            }
        }

        private async Task ReadCallback(MessageReader reader, EndPoint remote_end_point)
        {

            // I'm a little concerned about a infinite loop here, but it seems like it's possible 
            // to get 0 bytes read on UDP without the socket being shut down.
            if (reader.Length == 0)
            {
                reader.Recycle();
                logger?.WriteInfo("Received 0 bytes");
                Thread.Sleep(10);
                return;
            }

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
                    !HandshakeConnection((IPEndPoint)remote_end_point, reader, out var response))
                {
                    reader.Recycle();
                    if (response != null)
                    {
                        await SendData(response, response.Length, remote_end_point);
                    }

                    return;
                }

                aware = false;
                connection = new UdpServerConnection(this, (IPEndPoint)remote_end_point, IPMode, logger);
                all_connections.TryAdd(remote_end_point, connection);
            }

            // Inform the connection of the buffer (new connections need to send an ack back to client)
            await connection.HandleReceive(reader, reader.Length);

            if (!aware)
            {
                InvokeNewConnection(connection, reader);
            }
        }

        private void ManageReliablePackets(object? _state)
        {
            foreach (var connection in all_connections.Values)
            {
                connection.ManageReliablePackets();
            }

            try
            {
                reliable_packet_timer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        protected override void Dispose(bool _disposing)
        {
            foreach (var connection in all_connections.Values)
            {
                connection.Dispose();
            }

            Thread.Sleep(250); // give time to send all the disconnect messages

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
            try { socket.Dispose(); } catch { }

            reliable_packet_timer.Dispose();

            base.Dispose(_disposing);
        }

        internal void RemoveConnection(EndPoint _endpoint)
        {
            all_connections.TryRemove(_endpoint, out var conn);
        }

#if DEBUG
        public int TestDropRate = -1;
        private int drop_counter = 0;
#endif

        internal async Task SendData(byte[] _bytes, int _length, EndPoint _endpoint)
        {
            if (_length > _bytes.Length)
            {
                return;
            }

#if DEBUG
            if (TestDropRate > 0)
            {
                if (++drop_counter % TestDropRate == 0)
                {
                    return;
                }
            }
#endif

            try
            {
                var segment = new ArraySegment<byte>(_bytes, 0, _length);
                await socket.SendToAsync(_bytes, SocketFlags.None, _endpoint);

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

        internal void SendDataSync(byte[] _bytes, int _length, EndPoint _endpoint)
        {
            if (_length > _bytes.Length)
            {
                return;
            }

#if DEBUG
            if (TestDropRate > 0)
            {
                if (++drop_counter % TestDropRate == 0)
                {
                    return;
                }
            }
#endif

            try
            {
                var segment = new ArraySegment<byte>(_bytes, 0, _length);
                socket.SendTo(_bytes, SocketFlags.None, _endpoint);

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
    }
}