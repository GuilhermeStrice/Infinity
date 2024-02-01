using System.Net;

namespace Infinity.Core
{
    public abstract class NetworkConnection : IDisposable
    {
        public event Action<DataReceivedEvent>? DataReceived;
        public event Action<DisconnectedEvent>? Disconnected;

        public event EventHandler<MessageWriter>? BeforeSend;
        public event EventHandler<MessageReader>? BeforeReceive;

#if DEBUG
        public int TestLagMs = -1;
        public int TestDropRate = 0;
        protected int testDropCount = 0;
#endif

        public IPEndPoint? EndPoint { get; protected set; }

        public IPMode IPMode { get; protected set; }

        public Func<InfinityInternalErrors, MessageWriter>? OnInternalDisconnect;

        public virtual float AveragePingMs { get; }

        public ConnectionState State
        {
            get
            {
                return state;
            }

            protected set
            {
                state = value;
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

        public abstract SendErrors Send(MessageWriter _writer);

        public abstract void Connect(MessageWriter _writer, int _timeout = 5000);

        public abstract void ConnectAsync(MessageWriter _writer);

        public void Disconnect(string _reason, MessageWriter _writer)
        {
            if (SendDisconnect(_writer))
            {
                InvokeDisconnected(_reason, null);
            }

            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void InvokeDataReceived(MessageReader _reader)
        {
            if (DataReceived != null)
            {
                var @event = DataReceivedEvent.Get();
                @event.Connection = this;
                @event.Message = _reader;
                DataReceived.Invoke(@event);
            }
            else
            {
                if (_reader != null)
                {
                    _reader.Recycle();
                }
            }
        }

        protected void InvokeDisconnected(string _reason, MessageReader _reader)
        {
            if (Disconnected != null)
            {
                var @event = DisconnectedEvent.Get();
                @event.Connection = this;
                @event.Reason = _reason;
                @event.Message = _reader;
                Disconnected.Invoke(@event);
            }
            else
            {
                if (_reader != null)
                {
                    _reader.Recycle();
                }
            }
        }

        protected void InvokeBeforeSend(MessageWriter _writer)
        {
            BeforeSend?.Invoke(this, _writer);
        }

        protected void InvokeBeforeReceive(MessageReader _reader)
        {
            BeforeReceive?.Invoke(this, _reader);
        }

        protected abstract void DisconnectRemote(string _reason, MessageReader _reader);

        protected abstract void DisconnectInternal(InfinityInternalErrors _error, string _reason);

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
