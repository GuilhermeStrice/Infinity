using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Core.Udp
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

        ~UdpClientConnection()
        {
            Dispose(false);
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
                Dispose();
                throw new InfinityException("A SocketException occurred while initiating a receive operation.", e);
            }

            // Write bytes to the server to tell it hi (and to punch a hole in our NAT, if present)
            // When acknowledged set the state to connected
            SendHandshake(_writer, () =>
            {
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
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                // Already disposed and disconnected...
            }
            catch (SocketException ex)
            {
                DisconnectInternal(InfinityInternalErrors.SocketExceptionSend, "Could not send data as a SocketException occurred: " + ex.Message);
            }
        }

        private void HandleSendTo(IAsyncResult _result)
        {
            try
            {
                int sent = socket.EndSendTo(_result);
                Statistics.LogPacketSent(sent);
            }
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                // Already disposed and disconnected...
            }
            catch (SocketException ex)
            {
                DisconnectInternal(InfinityInternalErrors.SocketExceptionSend, "Could not send data as a SocketException occurred: " + ex.Message);
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
                reader.Recycle();
                Dispose();
            }
        }

        private void ReadCallback(IAsyncResult result)
        {
            var reader = (MessageReader)result.AsyncState;

            try
            {
                reader.Length = socket.EndReceive(result);
            }
            catch (SocketException e)
            {
                reader.Recycle();
                DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "Socket exception while reading data: " + e.Message);
                return;
            }
            catch (Exception e)
            {
                reader.Recycle();
                DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "No idea what happened here: " + e.Message);
                return;
            }

            //Exit if no bytes read, we've failed.
            if (reader.Length == 0)
            {
                reader.Recycle();
                DisconnectInternal(InfinityInternalErrors.ReceivedZeroBytes, "Received 0 bytes");
                return;
            }

            //Begin receiving again
            try
            {
                StartListeningForData();
            }
            catch (SocketException e)
            {
                DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "Socket exception during receive: " + e.Message);
            }
            catch (ObjectDisposedException)
            {
                //If the socket's been disposed then we can just end there.
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
            HandleReceive(reader, reader.Length);
        }

        protected override void SetState(ConnectionState _state)
        {
            try
            {
                // If the server disconnects you during the Handshake
                // you can go straight from Connecting to NotConnected.
                if (_state == ConnectionState.Connected
                    || _state == ConnectionState.NotConnected)
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

            lock (this)
            {
                if (State == ConnectionState.NotConnected)
                {
                    return false;
                }

                State = ConnectionState.NotConnected;
            }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var writer = UdpMessageFactory.BuildDisconnectMessage();
                SendDisconnect(writer);
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
            var _writer = UdpMessageFactory.BuildDisconnectMessage();
            if (SendDisconnect(_writer))
            {
                InvokeDisconnected(_reason, _reader);
            }

            Dispose();
        }
    }
}
