using Infinity.Core;

namespace Infinity.WebSockets
{
    public class WebSocketConnection : NetworkConnection
    {
        internal Timer? pingTimer;
        internal ILogger? logger;
        internal long lastPingTicks;
        internal volatile bool shuttingDown;
		internal volatile bool closeSent;
		internal volatile bool closeReceived;

        public WebSocketConnection()
        {
        }

        public override Task Connect(MessageWriter _writer, int _timeout = 5000)
        {
            throw new NotImplementedException();
        }

        public override Task<SendErrors> Send(MessageWriter _writer)
        {
            throw new NotImplementedException();
        }

        protected override async Task DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
            throw new NotImplementedException();
        }

        protected override async Task DisconnectRemote(string _reason, MessageReader _reader)
        {
            throw new NotImplementedException();
        }

        protected override bool SendDisconnect(MessageWriter _writer)
        {
            throw new NotImplementedException();
        }

        protected override void SetState(ConnectionState _state)
        {
            throw new NotImplementedException();
        }
    }
}