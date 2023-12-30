using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Infinity.Core
{
    public delegate bool HandshakeCheck(IPEndPoint endPoint, byte[] input, out byte[] response);

    public abstract class NetworkConnectionListener : IDisposable
    {
        public IPEndPoint ?EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        public int ReceiveBufferSize { get; set; } = 8096;

        public abstract double AveragePing { get; }
        public abstract int ConnectionCount { get; }

        public HandshakeCheck? HandshakeConnection;

        public event Action<NewConnectionEventArgs>? NewConnection;
        public event Action<InfinityInternalErrors>? OnInternalError;

        public abstract void Start();

        public void Dispose()
        {
            Dispose(true);
        }

        protected void InvokeNewConnection(NetworkConnection _connection, MessageReader _reader)
        {
            var args = new NewConnectionEventArgs(_connection, _reader);
            NewConnection?.Invoke(args);
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
