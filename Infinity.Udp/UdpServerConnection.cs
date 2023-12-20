using System.Net;

namespace Infinity.Core.Udp
{
    public sealed class UdpServerConnection : UdpConnection
    {
        public UdpConnectionListener Listener { get; private set; }

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

        public override void Connect(MessageWriter _writer, int _timeout = 5000)
        {
            NotClient();
        }

        public override void ConnectAsync(MessageWriter _writer)
        {
            NotClient();
        }

        protected override void SetState(ConnectionState _state)
        {
        }

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
