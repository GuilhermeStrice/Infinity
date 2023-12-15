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
        /// <param name="_listener">The listener that created this connection.</param>
        /// <param name="_endpoint">The endpoint that we are connected to.</param>
        /// <param name="IPMode">The IPMode we are connected using.</param>
        public UdpServerConnection(UdpConnectionListener _listener, IPEndPoint _endpoint, IPMode _ip_mode, ILogger _logger)
            : base(_logger)
        {
            Listener = _listener;
            EndPoint = _endpoint;
            IPMode = _ip_mode;

            State = ConnectionState.Connected;
            InitializeKeepAliveTimer();
        }

        public override void WriteBytesToConnection(byte[] _bytes, int _length)
        {
            Statistics.LogPacketSent(_length);
            Listener.SendData(_bytes, _length, EndPoint);
        }

        private void NotClient()
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        /// <remarks>
        ///     This will always throw
        /// </remarks>
        public override void Connect(MessageWriter _writer, int _timeout = 5000)
        {
            NotClient();
        }

        /// <remarks>
        ///     This will always throw
        /// </remarks>
        public override void ConnectAsync(MessageWriter _writer)
        {
            NotClient();
        }

        protected override void SetState(ConnectionState _state)
        {
        }

        /// <summary>
        ///     Sends a disconnect message to the end point.
        /// </summary>
        protected override bool SendDisconnect(MessageWriter _writer = null)
        {
            lock (this)
            {
                if (state != ConnectionState.Connected)
                {
                    return false;
                }

                state = ConnectionState.NotConnected;
            }
            
            var bytes = empty_disconnect_bytes;
            if (_writer != null && _writer.Length > 0)
            {
                bytes = _writer.ToByteArray(0);
                bytes[0] = UdpSendOption.Disconnect;
            }

            try
            {
                Listener.SendDataSync(bytes, bytes.Length, EndPoint);
            }
            catch { }

            return true;
        }

        protected override void Dispose(bool _disposing)
        {
            Listener.RemoveConnectionTo(EndPoint);

            SendDisconnect();

            base.Dispose(_disposing);
        }
    }
}
