using System.Net;

namespace Infinity.Core
{
    public delegate bool HandshakeCheck(IPEndPoint _endPoint, MessageReader _reader, out MessageWriter _response);

    public abstract class NetworkConnectionListener : IDisposable
    {
        public IPEndPoint ?EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        public int ReceiveBufferSize { get; set; } = 8096;

        public abstract double AveragePing { get; }
        public abstract int ConnectionCount { get; }

        public HandshakeCheck? HandshakeConnection;

        public event Action<NewConnectionEvent>? NewConnection;
        public event Action<InfinityInternalErrors>? OnInternalError;

        public abstract void Start();

        public void Dispose()
        {
            Dispose(true);
        }

        protected void InvokeNewConnection(NetworkConnection _connection, MessageReader _reader)
        {
            var @event = new NewConnectionEvent();
            @event.Connection = _connection;
            @event.HandshakeData = _reader;
            NewConnection?.Invoke(@event);
        }

        protected void InvokeInternalError(InfinityInternalErrors _error)
        {
            OnInternalError?.Invoke(_error);
        }

        protected virtual void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                NewConnection = null;
                OnInternalError = null;
            }
        }
    }
}
