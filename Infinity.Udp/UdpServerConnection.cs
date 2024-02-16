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

        public override void WriteBytesToConnectionSync(byte[] _bytes, int _length)
        {
            throw new NotImplementedException();
        }

        protected override void ShareConfiguration()
        {
            // Connection config
            MessageWriter writer = MessageWriter.Get();
            writer.Write(UdpSendOptionInternal.ShareConfiguration);

            writer.Position += 2;

            // Reliability
            writer.Write(configuration.ResendTimeoutMs);
            writer.Write(configuration.ResendLimit);
            writer.Write(configuration.ResendPingMultiplier);
            writer.Write(configuration.DisconnectTimeoutMs);

            // Keep Alive
            writer.Write(configuration.KeepAliveInterval);
            writer.Write(configuration.MissingPingsUntilDisconnect);

            // Fragmentation

            writer.Write(configuration.EnableFragmentation);

            byte[] buffer = new byte[writer.Length];

            Array.Copy(writer.Buffer, 0, buffer, 0, writer.Length);

            writer.Recycle();

            ReliableSend(buffer, () =>
            {
                InitializeKeepAliveTimer();
            });
        }

        protected override void ReadConfiguration(MessageReader _reader)
        {
            // do nothing here
        }

        protected override void Dispose(bool _disposing)
        {
            var writer = UdpMessageFactory.BuildDisconnectMessage();

            SendDisconnect(writer);

            writer.Recycle();

            Listener.RemoveConnection(EndPoint);

            base.Dispose(_disposing);
        }
    }
}
