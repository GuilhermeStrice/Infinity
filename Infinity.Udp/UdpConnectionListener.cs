using Infinity.Core;
using Infinity.Core.Exceptions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Infinity.Udp
{
    public class UdpConnectionListener : NetworkConnectionListener
    {
        private ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

        public UdpConnectionConfiguration Configuration { get; set; } = new UdpConnectionConfiguration(); // default config

        public UdpListenerStatistics Statistics { get; private set; }

        public override double AveragePing => all_connections.IsEmpty ? 0 : all_connections.Values.Average(c => c.AveragePingMs);
        public override int ConnectionCount => all_connections.Count;

        private const int send_receive_buffer_size = 1024;

        private Socket socket;
        private ILogger logger;

        private CancellationTokenSource cancellation_token_source = new CancellationTokenSource();

        private ConcurrentDictionary<EndPoint, UdpServerConnection> all_connections = new ConcurrentDictionary<EndPoint, UdpServerConnection>();

        private readonly Channel<(MessageReader, EndPoint)> _incoming = Channel.CreateUnbounded<(MessageReader, EndPoint)>();

        public UdpConnectionListener(IPEndPoint _endpoint, IPMode _ip_mode = IPMode.IPv4, ILogger _logger = null)
        {
            Statistics = new UdpListenerStatistics();

            logger = _logger;
            EndPoint = _endpoint;
            IPMode = _ip_mode;

            socket = CreateSocket(IPMode);

            socket.ReceiveBufferSize = send_receive_buffer_size;
            socket.SendBufferSize = send_receive_buffer_size;
            
            Util.FireAndForget(ManageReliablePackets(), logger);
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

        public void Stop()
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

            Util.FireAndForget(StartListeningForData(), logger);
            Util.FireAndForget(ProcessIncoming(), logger);
        }

        private async Task StartListeningForData()
        {
            while (!cancellation_token_source.IsCancellationRequested)
            {
                var reader = new MessageReader(allocator);
                
                EndPoint remote = IPMode == IPMode.IPv4
                    ? new IPEndPoint(IPAddress.Any, 0)
                    : new IPEndPoint(IPAddress.IPv6Any, 0);

                var manager = reader.AsManager();

                var result = await socket.ReceiveFromAsync(manager.Memory, SocketFlags.None, remote, cancellation_token_source.Token).ConfigureAwait(false);
                reader.Length = result.ReceivedBytes;
                reader.Position = 0;

                await _incoming.Writer.WriteAsync((reader, result.RemoteEndPoint)).ConfigureAwait(false);

                manager.Dispose();
            }
        }

        private async Task ProcessIncoming()
        {
            await foreach (var (reader, remote) in _incoming.Reader.ReadAllAsync(cancellation_token_source.Token).ConfigureAwait(false))
            {
                await ReadCallback(reader, remote).ConfigureAwait(false);
            }
        }

        private async Task ReadCallback(MessageReader reader, EndPoint remote_end_point)
        {
            if (!cancellation_token_source.IsCancellationRequested)
            {
                // I'm a little concerned about a infinite loop here, but it seems like it's possible 
                // to get 0 bytes read on UDP without the socket being shut down.
                if (reader.Length == 0)
                {
                    logger?.WriteInfo("Received 0 bytes");
                    await Task.Delay(10).ConfigureAwait(false);
                    return;
                }

                bool aware = true;
                bool is_handshake = reader[0] == UdpSendOptionInternal.Handshake;

                // If we're aware of this connection use the one already
                // If this is a new client then connect with them!
                UdpServerConnection connection;
                if (!all_connections.TryGetValue(remote_end_point, out connection))
                {
                    // Check for malformed connection attempts
                    if (!is_handshake)
                    {
                        return;
                    }

                    if (HandshakeConnection != null &&
                        !HandshakeConnection((IPEndPoint)remote_end_point, reader, out var response))
                    {
                        await SendData(response, remote_end_point).ConfigureAwait(false);

                        return;
                    }

                    aware = false;
                    connection = new UdpServerConnection(this, (IPEndPoint)remote_end_point, IPMode, logger);
                    all_connections.TryAdd(remote_end_point, connection);
                }

                // Inform the connection of the buffer (new connections need to send an ack back to client)
                await connection.HandleReceive(reader, reader.Length).ConfigureAwait(false);

                if (!aware)
                {
                    InvokeNewConnection(connection, reader);
                }
            }
        }

        private async Task ManageReliablePackets()
        {
            while (!cancellation_token_source.IsCancellationRequested)
            {
                try
                {
                    foreach (var connection in all_connections.Values)
                    {
                        Util.FireAndForget(connection.ManageReliablePackets(), logger);
                    }
                }
                catch (Exception ex)
                {
                    logger?.WriteError($"ManageReliablePackets error: {ex}");
                }

                await Task.Delay(100, cancellation_token_source.Token).ConfigureAwait(false);
            }
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

        internal async Task SendData(MessageWriter _writer, EndPoint _endpoint)
        {
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
                var manager = _writer.AsManager();

                await socket.SendToAsync(manager.Memory, SocketFlags.None, _endpoint);

                Statistics.AddBytesSent(_writer.Length);

                manager.Dispose();
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

        internal void SendDataSync(MessageWriter _writer, EndPoint _endpoint)
        {
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
                socket.SendTo(_writer.Buffer, SocketFlags.None, _endpoint);

                Statistics.AddBytesSent(_writer.Length);
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