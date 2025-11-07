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

            _ = BootstrapMTU();
        }

        public override void WriteBytesToConnectionSync(MessageWriter _writer)
        {
            try
            {
                Statistics.LogPacketSent(_writer.Length);
                Listener.SendDataSync(_writer.Buffer, _writer.Length, EndPoint);
            }
            finally
            {
                _writer.Recycle();
            }
        }

        public override async Task WriteBytesToConnection(MessageWriter _writer)
        {
            Statistics.LogPacketSent(_writer.Length);
            await Listener.SendData(_writer.Buffer, _writer.Length, EndPoint).ConfigureAwait(false);
        }

        public override async Task Connect(MessageWriter _writer, int _timeout = 5000)
        {
            NotClient();
        }

        private void NotClient()
        {
            throw new InvalidOperationException("Cannot manually connect a UdpServerConnection, did you mean to use UdpClientConnection?");
        }

        protected override void SetState(ConnectionState _state)
        {
            state = _state;
        }

        protected override bool SendDisconnect(MessageWriter _writer)
        {
            SendSync(_writer);

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

        protected override async Task ShareConfiguration()
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

            await ReliableSend(writer, () =>
            {
                InitializeKeepAliveTimer();
            }).ConfigureAwait(false);
        }

        protected override async Task ReadConfiguration(MessageReader _reader)
        {
            // do nothing here
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
    }
}
