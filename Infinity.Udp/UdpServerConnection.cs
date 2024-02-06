using Infinity.Core;
using System.Net;

namespace Infinity.Udp
{
    public sealed class UdpServerConnection : UdpConnection
    {
        public UdpConnectionListener Listener { get; private set; }

        public UdpServerConnection(UdpConnectionListener _listener, IPEndPoint _endpoint, IPMode _ip_mode, ILogger _logger)
            : base(_logger)
        {
            configuration = _listener.Configuration;
            Listener = _listener;
            EndPoint = _endpoint;
            IPMode = _ip_mode;

            State = ConnectionState.Connected;
            InitializeKeepAliveTimer();

            BootstrapMTU();
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

        protected override void Dispose(bool _disposing)
        {
            if (State == ConnectionState.Connected)
            {
                var writer = UdpMessageFactory.BuildDisconnectMessage();

                SendDisconnect(writer);

                writer.Recycle();
            }

            Listener.RemoveConnection(EndPoint);

            base.Dispose(_disposing);
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
