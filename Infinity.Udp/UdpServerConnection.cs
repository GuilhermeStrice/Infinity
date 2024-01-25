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

        public override void Connect(MessageWriter _writer, int _timeout = 5000)
        {
            NotClient();
        }

        public override void ConnectAsync(MessageWriter _writer)
        {
            NotClient();
        }

        private void NotClient()
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        protected override void SetState(ConnectionState _state)
        {
        }

        protected override bool SendDisconnect(MessageWriter _writer = null)
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

        protected override void Dispose(bool _disposing)
        {
            Listener.RemoveConnectionTo(EndPoint);

            SendDisconnect();

            base.Dispose(_disposing);
        }
    }
}
