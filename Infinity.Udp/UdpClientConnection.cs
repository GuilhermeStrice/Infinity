using Infinity.Core;
using Infinity.Core.Exceptions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Infinity.Udp
{
    public sealed class UdpClientConnection : UdpConnection
    {
        private Socket socket;

        private ManualResetEvent connect_wait_lock = new ManualResetEvent(false);

        private CancellationTokenSource cancellation_token_source = new CancellationTokenSource();

        public UdpClientConnection(ILogger _logger, IPEndPoint _remote_end_point, IPMode _ip_mode = IPMode.IPv4)
            : base(_logger)
        {
            EndPoint = _remote_end_point;
            IPMode = _ip_mode;

            socket = CreateSocket(_ip_mode);

            Util.FireAndForget(ManageReliablePacketsInternal(), logger);
        }

        private Socket CreateSocket(IPMode _ip_mode)
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

            socket.Blocking = false;

            return socket;
        }

        ~UdpClientConnection()
        {
            Dispose(true);
        }

        public override async Task WriteBytesToConnection(MessageWriter _writer)
        {
#if DEBUG
            if (TestLagMs > 0)
            {
                Thread.Sleep(TestLagMs);
                await WriteBytesToConnectionReal(_writer.Buffer, _writer.Length).ConfigureAwait(false);
            }
            else
#endif
            {
                await WriteBytesToConnectionReal(_writer.Buffer, _writer.Length).ConfigureAwait(false);
                await WriteBytesToConnectionReal(_writer.Buffer, _writer.Length).ConfigureAwait(false);
            }
        }

        public override void WriteBytesToConnectionSync(MessageWriter _writer)
        {
            try
            {
                int sent = socket.SendTo(_writer.Buffer, _writer.Length, SocketFlags.None, EndPoint);
                Statistics.LogPacketSent(sent);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.MessageSize)
            {
                FinishMTUExpansion();
            }
            catch
            {
                // this is handles by keep alive and packet resends
            }
            finally
            {
                _writer.Recycle();
            }
        }

        public override async Task Connect(MessageWriter _writer, int _timeout = 5000)
        {
            State = ConnectionState.Connecting;

            try
            {
                if (IPMode == IPMode.IPv4)
                {
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                }
                else
                {
                    socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
                }
            }
            catch (SocketException e)
            {
                State = ConnectionState.NotConnected;
                throw new InfinityException("A SocketException occurred while binding to the port.", e);
            }

            Util.FireAndForget(StartListeningForData(), logger);

            // Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            await SendHandshake(_writer, async () =>
            {
                await BootstrapMTU().ConfigureAwait(false);

                await AskConfiguration().ConfigureAwait(false);
            }).ConfigureAwait(false);

            //Wait till Handshake packet is acknowledged and the state is set to Connected
            bool timed_out = !connect_wait_lock.WaitOne(_timeout);

            //If we timed out raise an exception
            if (timed_out)
            {
                Dispose();
                throw new InfinityException("Connection attempt timed out.");
            }
        }

        private async Task WriteBytesToConnectionReal(byte[] _bytes, int _length)
        {
            try
            {
                var segment = new ArraySegment<byte>(_bytes, 0, _length);
                int sent = await socket.SendToAsync(segment, SocketFlags.None, EndPoint).ConfigureAwait(false);
                Statistics.LogPacketSent(sent);
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.MessageSize)
            {
                FinishMTUExpansion();
            }
            catch
            {
                // this is handles by keep alive and packet resends
            }
        }

        private async Task SendHandshake(MessageWriter _writer, Action _acknowledge_callback)
        {
            await ReliableSend(_writer, _acknowledge_callback).ConfigureAwait(false);
        }

        private async Task AskConfiguration()
        {
            var writer = UdpMessageFactory.BuildAskConfirurationMessage();

            await ReliableSend(writer).ConfigureAwait(false);
        }

        private async Task ManageReliablePacketsInternal()
        {
            while (!cancellation_token_source.IsCancellationRequested)
            {
                await ManageReliablePackets().ConfigureAwait(false);
                await Task.Delay(100, cancellation_token_source.Token).ConfigureAwait(false);
            }
        }

        private async Task StartListeningForData()
        {
            while (!cancellation_token_source.IsCancellationRequested)
            {
#if DEBUG
                if (TestLagMs > 0)
                {
                    Thread.Sleep(TestLagMs);
                }
#endif

                var reader = MessageReader.Get();
                try
                {
                    var bytes_received = await socket.ReceiveAsync(reader.Buffer, SocketFlags.None).ConfigureAwait(false);
                    reader.Length = bytes_received;
                    reader.Position = 0;

                    await ReadCallback(reader).ConfigureAwait(false);
                }
                catch
                {
                    // this is handles by keep alive and packet resends
                    reader.Recycle();
                }
            }
        }

        private async Task ReadCallback(MessageReader reader)
        {
            //Exit if no bytes read, we've failed.
            if (reader.Length == 0)
            {
                reader.Recycle();
                return;
            }

#if DEBUG
            if (TestDropRate > 0)
            {
                if ((testDropCount++ % TestDropRate) == 0)
                {
                    return;
                }
            }
#endif
            await HandleReceive(reader, reader.Length).ConfigureAwait(false);
        }

        protected override void SetState(ConnectionState _state)
        {
            state = _state;

            if (_state == ConnectionState.Connected || _state == ConnectionState.NotConnected)
            {
                connect_wait_lock.Set();
            }
            else
            {
                connect_wait_lock.Reset();
            }
        }

        protected override bool SendDisconnect(MessageWriter _writer)
        {
            SendSync(_writer);

            if (State == ConnectionState.NotConnected)
            {
                return false;
            }

            State = ConnectionState.NotConnected;

            return true;
        }

        protected override void DisconnectRemote(string _reason, MessageReader _reader)
        {
            var writer = UdpMessageFactory.BuildDisconnectMessage();
            if (SendDisconnect(writer))
            {
                InvokeDisconnected(_reason, _reader);
            }

            writer.Recycle();

            Dispose();
        }

        protected override void DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
            var msg = OnInternalDisconnect?.Invoke(_error);

            if (msg == null)
            {
                msg = UdpMessageFactory.BuildDisconnectMessage();
            }

            Disconnect(_reason, msg);
        }

        protected override async Task ShareConfiguration()
        {
            // do nothing here
        }

        protected override async Task ReadConfiguration(MessageReader _reader)
        {
            // Read Config
            _reader.Position += 3;

            // Reliability
            configuration.ResendTimeoutMs = _reader.ReadInt32();
            configuration.ResendLimit = _reader.ReadInt32();
            configuration.ResendPingMultiplier = _reader.ReadSingle();
            configuration.DisconnectTimeoutMs = _reader.ReadInt32();

            // Keep Alive
            configuration.KeepAliveInterval = _reader.ReadInt32();
            configuration.MissingPingsUntilDisconnect = _reader.ReadInt32();

            // Fragmentation
            configuration.EnableFragmentation = _reader.ReadBoolean();

            State = ConnectionState.Connected;

            InitializeKeepAliveTimer();

            await DiscoverMTU().ConfigureAwait(false);
        }

        protected override void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                try
                {
                    if (State != ConnectionState.NotConnected)
                    {
                        // send disconnect packet to server
                        var writer = UdpMessageFactory.BuildDisconnectMessage();
                        SendDisconnect(writer);
                        writer.Recycle();

                        // Fire client-side Disconnected event
                        InvokeDisconnected("Disposed", null);
                    }
                }
                catch
                {
                    // swallow exceptions
                }
            }

            cancellation_token_source?.Cancel();

            try { socket?.Shutdown(SocketShutdown.Both); } catch { }
            try { socket?.Close(); } catch { }
            try { socket?.Dispose(); } catch { }

            socket = null;

            base.Dispose(_disposing);
        }
    }
}
