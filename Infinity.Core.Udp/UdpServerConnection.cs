using System.Net;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Represents a servers's connection to a client that uses the UDP protocol.
    /// </summary>
    public sealed class UdpServerConnection : UdpConnection
    {
        /// <summary>
        ///     The connection listener that we use the socket of.
        /// </summary>
        /// <remarks>
        ///     Udp server connections utilize the same socket in the listener for sends/receives, this is the listener that 
        ///     created this connection and is hence the listener this conenction sends and receives via.
        /// </remarks>
        public UdpConnectionListener Listener { get; private set; }

        /// <summary>
        ///     Creates a UdpConnection for the virtual connection to the endpoint.
        /// </summary>
        /// <param name="listener">The listener that created this connection.</param>
        /// <param name="endPoint">The endpoint that we are connected to.</param>
        /// <param name="IPMode">The IPMode we are connected using.</param>
        public UdpServerConnection(UdpConnectionListener listener, IPEndPoint endPoint, IPMode ipMode, ILogger logger)
            : base(logger)
        {
            Listener = listener;
            EndPoint = endPoint;
            IPMode = ipMode;

            State = ConnectionState.Connected;
            InitializeKeepAliveTimer();
        }

        public override void WriteBytesToConnection(byte[] bytes, int length)
        {
            Statistics.LogPacketSent(length);
            Listener.SendData(bytes, length, EndPoint);
        }

        /// <remarks>
        ///     This will always throw
        /// </remarks>
        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        /// <remarks>
        ///     This will always throw
        /// </remarks>
        public override void ConnectAsync(byte[] bytes = null)
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter data = null)
        {
            lock (this)
            {
                if (_state != ConnectionState.Connected)
                    return false;

                _state = ConnectionState.NotConnected;
            }
            
            var bytes = EmptyDisconnectBytes;
            if (data != null && data.Length > 0)
            {
                if (data.SendOption != UdpSendOption.Unreliable)
                    throw new ArgumentException("Disconnect messages can only be unreliable.");

                bytes = data.ToByteArray(true);
                bytes[0] = UdpSendOption.Disconnect;
            }

            try
            {
                Listener.SendDataSync(bytes, bytes.Length, EndPoint);
            }
            catch { }

            return true;
        }

        protected override void Dispose(bool disposing)
        {
            Listener.RemoveConnectionTo(EndPoint);

            SendDisconnect();

            if (disposing)
            {
                SendDisconnect();
            }

            base.Dispose(disposing);
        }
    }
}
