using Infinity.Core;
using Infinity.Core.Exceptions;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Udp
{
    public sealed class UdpClientConnection : UdpConnection
    {
        private Socket socket;

        private ManualResetEvent connect_wait_lock = new ManualResetEvent(false);

        private Timer reliable_packet_timer;

        public UdpClientConnection(ILogger _logger, IPEndPoint _remote_end_point, IPMode _ip_mode = IPMode.IPv4)
            : base(_logger)
        {
            EndPoint = _remote_end_point;
            IPMode = _ip_mode;

            socket = CreateSocket(Protocol.Udp, _ip_mode);

            reliable_packet_timer = new Timer(ManageReliablePacketsInternal, null, 100, Timeout.Infinite);
            InitializeKeepAliveTimer();

            VerifyMTU();
        }

        ~UdpClientConnection()
        {
            Dispose(false);
        }

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

        public override void WriteBytesToConnection(byte[] _bytes, int _length)
        {
#if DEBUG
            if (TestLagMs > 0)
            {
                ThreadPool.QueueUserWorkItem(a => { Thread.Sleep(TestLagMs); WriteBytesToConnectionReal(_bytes, _length); });
            }
            else
#endif
            {
                WriteBytesToConnectionReal(_bytes, _length);
            }
        }

        public override void Connect(MessageWriter _writer, int _timeout = 5000)
        {
            ConnectAsync(_writer);

            //Wait till Handshake packet is acknowledged and the state is set to Connected
            bool timed_out = !connect_wait_lock.WaitOne(_timeout);

            //If we timed out raise an exception
            if (timed_out)
            {
                Dispose();
                throw new InfinityException("Connection attempt timed out.");
            }
        }

        public override void ConnectAsync(MessageWriter _writer)
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

            try
            {
                StartListeningForData();
            }
            catch (ObjectDisposedException)
            {
                // If the socket's been disposed then we can just end there but make sure we're in NotConnected state.
                // If we end up here I'm really lost...
                State = ConnectionState.NotConnected;
                return;
            }
            catch (SocketException e)
            {
                throw new InfinityException("A SocketException occurred while initiating a receive operation.", e);
            }

            // Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            // When acknowledged set the state to connected
            SendHandshake(_writer, () =>
            {
                DiscoverMTU();

                State = ConnectionState.Connected;
                ResetKeepAliveTimer();
            });
        }

        private void WriteBytesToConnectionReal(byte[] _bytes, int _length)
        {
            try
            {
                socket.BeginSendTo(
                    _bytes,
                    0,
                    _length,
                    SocketFlags.None,
                    EndPoint,
                    HandleSendTo,
                    null);
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

        private void HandleSendTo(IAsyncResult _result)
        {
            try
            {
                int sent = socket.EndSendTo(_result);
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

        private void ManageReliablePacketsInternal(object? _state)
        {
            ManageReliablePackets();
            try
            {
                reliable_packet_timer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        private void StartListeningForData()
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
                socket.BeginReceive(reader.Buffer, 0, reader.Buffer.Length, SocketFlags.None, ReadCallback, reader);
            }
            catch
            {
                // this is handles by keep alive and packet resends
                reader.Recycle();
            }
        }

        private void ReadCallback(IAsyncResult result)
        {
            var reader = (MessageReader)result.AsyncState;

            bool process = true;

            try
            {
                reader.Length = socket.EndReceive(result);
            }
            catch
            {
                // this is handles by keep alive and packet resends
                reader.Recycle();
                process = false;
            }

            //Exit if no bytes read, we've failed.
            if (reader.Length == 0)
            {
                reader.Recycle();
                return;
            }

            //Begin receiving again
            StartListeningForData();

#if DEBUG
            if (TestDropRate > 0)
            {
                if ((testDropCount++ % TestDropRate) == 0)
                {
                    return;
                }
            }
#endif
            if (process)
            {
                HandleReceive(reader, reader.Length);
            }
        }

        protected override void SetState(ConnectionState _state)
        {
            try
            {
                // If the server disconnects you during the Handshake
                // you can go straight from Connecting to NotConnected.
                if (_state == ConnectionState.Connected || _state == ConnectionState.NotConnected)
                {
                    connect_wait_lock.Set();
                }
                else
                {
                    connect_wait_lock.Reset();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        protected override bool SendDisconnect(MessageWriter _writer)
        {
            Send(_writer);

            if (State == ConnectionState.NotConnected)
            {
                return false;
            }

            State = ConnectionState.NotConnected;

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var writer = UdpMessageFactory.BuildDisconnectMessage();
                SendDisconnect(writer);
                writer.Recycle();
            }

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
            try { socket.Dispose(); } catch { }

            reliable_packet_timer.Dispose();
            connect_wait_lock.Dispose();

            base.Dispose(disposing);
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
    }
}
