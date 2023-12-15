using System.Net;
using System.Net.Sockets;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Represents a client's connection to a server that uses the UDP protocol.
    /// </summary>
    public sealed class UdpClientConnection : UdpConnection
    {
        public int ReceiveBufferSize = 8096;

        /// <summary>
        ///     The socket we're connected via.
        /// </summary>
        private Socket socket;

        /// <summary>
        ///     Reset event that is triggered when the connection is marked Connected.
        /// </summary>
        private ManualResetEvent connect_wait_lock = new ManualResetEvent(false);

        private Timer reliable_packet_timer;

        /// <summary>
        ///     Creates a new UdpClientConnection.
        /// </summary>
        /// <param name="_remote_end_point">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public UdpClientConnection(ILogger _logger, IPEndPoint _remote_end_point, IPMode _ip_mode = IPMode.IPv4)
            : base(_logger)
        {
            EndPoint = _remote_end_point;
            IPMode = _ip_mode;

            socket = CreateSocket(Protocol.Udp, _ip_mode);

            reliable_packet_timer = new Timer(ManageReliablePacketsInternal, null, 100, Timeout.Infinite);
            InitializeKeepAliveTimer();
        }
        
        ~UdpClientConnection()
        {
            Dispose(false);
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
                InitializeKeepAliveTimer();
            });
        }

        /// <summary>
        ///     Instructs the listener to begin listening.
        /// </summary>
        void StartListeningForData()
        {
#if DEBUG
            if (TestLagMs > 0)
            {
                Thread.Sleep(TestLagMs);
            }
#endif

            var reader = MessageReader.GetSized(ReceiveBufferSize);
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

        /// <summary>
        ///     Called when data has been received by the socket.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void ReadCallback(IAsyncResult result)
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

        /// <summary>
        ///     Sends a disconnect message to the end point.
        ///     You may include optional disconnect data. The SendOption must be unreliable.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter _writer = null)
        {
            lock (this)
            {
                if (State == ConnectionState.NotConnected) 
                {
                    return false;
                }

                State = ConnectionState.NotConnected;
            }

            var bytes = empty_disconnect_bytes;
            if (_writer != null && _writer.Length > 0)
            {
                bytes = _writer.ToByteArray(3);
            }

            bytes[0] = UdpSendOption.Disconnect;

            try
            {
                socket.SendTo(
                    bytes,
                    0,
                    bytes.Length,
                    SocketFlags.None,
                    EndPoint);
            }
            catch { }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SendDisconnect();
            }

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
            try { socket.Dispose(); } catch { }

            reliable_packet_timer.Dispose();
            connect_wait_lock.Dispose();

            base.Dispose(disposing);
        }
    }
}
