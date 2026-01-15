using System.Net;

namespace Infinity.Core
{
    public abstract class NetworkConnection : IDisposable
    {
        public delegate Task AsyncEventHandler<TEventArgs>(TEventArgs e);
        public delegate Task BeforeEventHandler<TEventArgs>(object? sender, TEventArgs e);

        public event AsyncEventHandler<DataReceivedEvent>? DataReceived;
        public event AsyncEventHandler<DisconnectedEvent>? Disconnected;

        public event BeforeEventHandler<MessageWriter>? BeforeSend;
        public event BeforeEventHandler<MessageReader>? BeforeReceive;

#if DEBUG
        public int TestLagMs = -1;
        public int TestDropRate = 0;
        protected int testDropCount = 0;
#endif

        public IPEndPoint? EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        public Func<InfinityInternalErrors, MessageWriter>? OnInternalDisconnect;

        public virtual float AveragePingMs { get; protected set; }

        public ConnectionState State
        {
            get
            {
                return state;
            }

            protected set
            {
                SetState(value);
            }
        }

        protected ConnectionState state;

        protected NetworkConnection()
        {
            State = ConnectionState.NotConnected;
        }

        public long GetIP4Address()
        {
            if (IPMode == IPMode.IPv4)
            {
                return EndPoint.Address.Address;
            }
            else
            {
                var bytes = EndPoint.Address.GetAddressBytes();
                return BitConverter.ToInt64(bytes, bytes.Length - 8);
            }
        }

        public abstract Task<SendErrors> Send(MessageWriter _writer);

        public abstract Task Connect(MessageWriter _writer, int _timeout = 5000);

        public async Task Disconnect(string _reason, MessageWriter _writer)
        {
            if (SendDisconnect(_writer))
            {
                await InvokeDisconnected(_reason, new MessageReader(new ChunkAllocator(1024))).ConfigureAwait(false);
            }

            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected async Task InvokeDataReceived(MessageReader _reader)
        {
            if (DataReceived != null)
            {
                var @event = new DataReceivedEvent();
                @event.Connection = this;
                @event.Message = _reader;
                await DataReceived.Invoke(@event).ConfigureAwait(false);
            }
        }

        protected async Task InvokeDisconnected(string _reason, MessageReader _reader)
        {
            if (Disconnected != null)
            {
                var @event = new DisconnectedEvent();
                @event.Connection = this;
                @event.Reason = _reason;
                @event.Message = _reader;
                await Disconnected.Invoke(@event).ConfigureAwait(false);
            }
        }

        protected async Task InvokeBeforeSend(MessageWriter _writer)
        {
            if (BeforeSend != null)
            {
                await BeforeSend.Invoke(this, _writer).ConfigureAwait(false);
            }
        }

        protected async Task InvokeBeforeReceive(MessageReader _reader)
        {
            if (BeforeReceive != null)
            {
                await BeforeReceive.Invoke(this, _reader).ConfigureAwait(false);
            }
        }

        protected abstract Task DisconnectRemote(string _reason, MessageReader _reader);

        protected abstract Task DisconnectInternal(InfinityInternalErrors _error, string _reason);

        protected abstract bool SendDisconnect(MessageWriter _writer);

        protected abstract void SetState(ConnectionState _state);

        protected virtual void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                DataReceived = null;
                Disconnected = null;
            }
        }
    }
}
