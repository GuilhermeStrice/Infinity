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
        private ManualResetEvent connectWaitLock = new ManualResetEvent(false);

        private Timer reliablePacketTimer;

        /// <summary>
        ///     Creates a new UdpClientConnection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public UdpClientConnection(ILogger logger, IPEndPoint remoteEndPoint, IPMode ipMode = IPMode.IPv4)
            : base(logger)
        {
            EndPoint = remoteEndPoint;
            IPMode = ipMode;

            socket = CreateSocket(Protocol.Udp, ipMode);

            reliablePacketTimer = new Timer(ManageReliablePacketsInternal, null, 100, Timeout.Infinite);
            InitializeKeepAliveTimer();
        }
        
        ~UdpClientConnection()
        {
            Dispose(false);
        }

        private void ManageReliablePacketsInternal(object state)
        {
            ManageReliablePackets();
            try
            {
                reliablePacketTimer.Change(100, Timeout.Infinite);
            }
            catch { }
        }

        public override void WriteBytesToConnection(byte[] bytes, int length)
        {
#if DEBUG
            if (TestLagMs > 0)
            {
                ThreadPool.QueueUserWorkItem(a => { Thread.Sleep(TestLagMs); WriteBytesToConnectionReal(bytes, length); });
            }
            else
#endif
            {
                WriteBytesToConnectionReal(bytes, length);
            }
        }

        private void WriteBytesToConnectionReal(byte[] bytes, int length)
        {
            try
            {
                Statistics.LogPacketSent(length);
                socket.BeginSendTo(
                    bytes,
                    0,
                    length,
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

        private void HandleSendTo(IAsyncResult result)
        {
            try
            {
                socket.EndSendTo(result);
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

        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            ConnectAsync(bytes);

            //Wait till Handshake packet is acknowledged and the state is set to Connected
            bool timedOut = !connectWaitLock.WaitOne(timeout);

            //If we timed out raise an exception
            if (timedOut)
            {
                Dispose();
                throw new InfinityException("Connection attempt timed out.");
            }
        }

        public override void ConnectAsync(byte[] bytes = null)
        {
            State = ConnectionState.Connecting;

            try
            {
                if (IPMode == IPMode.IPv4)
                    socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                else
                    socket.Bind(new IPEndPoint(IPAddress.IPv6Any, 0));
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
            SendHandshake(bytes, () =>
            {
                SetState(ConnectionState.Connected);
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

            var msg = MessageReader.GetSized(ReceiveBufferSize);
            try
            {
                socket.BeginReceive(msg.Buffer, 0, msg.Buffer.Length, SocketFlags.None, ReadCallback, msg);
            }
            catch
            {
                msg.Recycle();
                Dispose();
            }
        }

        protected override void SetState(ConnectionState state)
        {
            try
            {
                // If the server disconnects you during the Handshake
                // you can go straight from Connecting to NotConnected.
                if (state == ConnectionState.Connected
                    || state == ConnectionState.NotConnected)
                {
                    connectWaitLock.Set();
                }
                else
                {
                    connectWaitLock.Reset();
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        /// <summary>
        ///     Called when data has been received by the socket.
        /// </summary>
        /// <param name="result">The asyncronous operation's result.</param>
        void ReadCallback(IAsyncResult result)
        {
            var msg = (MessageReader)result.AsyncState;

            try
            {
                msg.Length = socket.EndReceive(result);
            }
            catch (SocketException e)
            {
                msg.Recycle();
                DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "Socket exception while reading data: " + e.Message);
                return;
            }
            catch (Exception e)
            {
                msg.Recycle();
                DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "No idea what happened here: " + e.Message);
                return;
            }

            //Exit if no bytes read, we've failed.
            if (msg.Length == 0)
            {
                msg.Recycle();
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
            HandleReceive(msg, msg.Length);
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        ///     You may include optional disconnect data. The SendOption must be unreliable.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter data = null)
        {
            lock (this)
            {
                if (_state == ConnectionState.NotConnected) 
                    return false;
                State = ConnectionState.NotConnected;
            }

            var bytes = EmptyDisconnectBytes;
            if (data != null && data.Length > 0)
            {
                if (data.SendOption != UdpSendOption.Unreliable)
                    throw new ArgumentException("Disconnect messages can only be unreliable.");

                bytes = data.ToByteArray(3);
                bytes[0] = UdpSendOption.Disconnect;
            }

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
                SendDisconnect();

            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
            try { socket.Dispose(); } catch { }

            reliablePacketTimer.Dispose();
            connectWaitLock.Dispose();

            base.Dispose(disposing);
        }
    }
}
