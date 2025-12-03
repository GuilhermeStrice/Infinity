using System.Threading.Channels;
using Infinity.Core;
using Infinity.Core.Exceptions;

namespace Infinity.Udp
{
    public abstract partial class UdpConnection : NetworkConnection
    {
        public UdpConnectionConfiguration Configuration 
        { 
            get
            {
                return configuration.Clone();
            }
        }

        public UdpConnectionStatistics Statistics { get; private set; } = new UdpConnectionStatistics();

        protected readonly ILogger logger;

        internal UdpConnectionConfiguration configuration;

        private readonly Channel<MessageWriter> _outgoing = Channel.CreateUnbounded<MessageWriter>();
        internal CancellationTokenSource cancellation_token_source = new CancellationTokenSource();

        public UdpConnection(ILogger _logger) : base()
        {
            configuration = new UdpConnectionConfiguration();
            logger = _logger;

            Util.FireAndForget(SendInternal(), logger);
        }

        public abstract Task WriteBytesToConnection(MessageWriter _writer, bool _recycle_writer = true);
        public abstract void WriteBytesToConnectionSync(MessageWriter _writer);

        protected abstract Task ShareConfiguration();
        protected abstract Task ReadConfiguration(MessageReader _reader);

        public SendErrors SendSync(MessageWriter _writer)
        {
            if (state != ConnectionState.Connected)
            {
                return SendErrors.Disconnected;
            }

            WriteBytesToConnectionSync(_writer);

            return SendErrors.None;
        }

        public override async Task<SendErrors> Send(MessageWriter _writer)
        {
            await _outgoing.Writer.WriteAsync(_writer, cancellation_token_source.Token);
            return SendErrors.None;
        }

        public async Task SendInternal()
        {
            await foreach (var _writer in _outgoing.Reader.ReadAllAsync(cancellation_token_source.Token).ConfigureAwait(false))
            {
                if (state != ConnectionState.Connected)
                {
                    return;
                }

                await InvokeBeforeSend(_writer).ConfigureAwait(false);

                switch (_writer.Buffer[0])
                {
                    case UdpSendOption.Reliable:
                        {
                            if (_writer.Length > MTU)
                            {
                                throw new InfinityException("not allowed");
                            }

                            await ReliableSend(_writer).ConfigureAwait(false);

                            break;
                        }
                    case UdpSendOption.ReliableOrdered:
                        {
                            if (_writer.Length > MTU)
                            {
                                throw new InfinityException("not allowed");
                            }

                            await OrderedSend(_writer).ConfigureAwait(false);
                            Statistics.LogReliableMessageSent(_writer.Length);

                            break;
                        }
                    case UdpSendOption.Fragmented:
                        {
                            if (configuration.EnableFragmentation)
                            {
                                if (_writer.Length <= MTU)
                                {
                                    throw new InfinityException("Message not big enough");
                                }

                                await FragmentedSend(_writer).ConfigureAwait(false);

                                Statistics.LogFragmentedMessageSent(_writer.Length);
                            }
                            else
                            {
                                throw new InfinityException("Enable fragmentation to use fragmented messages");
                            }

                            break;
                        }
                    default: // applies to disconnect and unreliable
                        {
                            Statistics.LogUnreliableMessageSent(_writer.Length);
                            await WriteBytesToConnection(_writer).ConfigureAwait(false);

                            break;
                        }
                }
            }
        }

        protected internal virtual async Task HandleReceive(MessageReader _reader, int _bytes_received)
        {
            switch (_reader.Buffer[0])
            {
                //Handle reliable receives
                case UdpSendOption.Reliable:
                    {
                        await InvokeBeforeReceive(_reader).ConfigureAwait(false);
                        await ReliableMessageReceive(_reader).ConfigureAwait(false);
                        Statistics.LogReliableMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOption.ReliableOrdered:
                    {
                        await InvokeBeforeReceive(_reader).ConfigureAwait(false);
                        await OrderedMessageReceived(_reader).ConfigureAwait(false);
                        Statistics.LogReliableMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOptionInternal.Acknowledgement:
                    {
                        AcknowledgementMessageReceive(_reader.Buffer, _bytes_received);
                        Statistics.LogAcknowledgementReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOptionInternal.Ping:
                    {
                        await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
                        Statistics.LogPingReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOptionInternal.Handshake:
                    {
                        await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
                        Statistics.LogHandshakeReceived(_bytes_received);
                        break;
                    }

                case UdpSendOptionInternal.Fragment:
                    {
                        await FragmentMessageReceive(_reader).ConfigureAwait(false);
                        Statistics.LogFragmentedMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOptionInternal.TestMTU:
                    {
                        await MTUTestReceive(_reader).ConfigureAwait(false);
                        Statistics.LogMTUTestMessageReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                    // sent by client
                case UdpSendOptionInternal.AskConfiguration:
                    {
                        await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
                        await ShareConfiguration().ConfigureAwait(false);
                        _reader.Recycle();
                        break;
                    }

                    // sent by server
                case UdpSendOptionInternal.ShareConfiguration:
                    {
                        await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
                        await ReadConfiguration(_reader).ConfigureAwait(false);
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOption.Disconnect:
                    {
                        _reader.Position = 1;
                        await DisconnectRemote("The remote sent a disconnect request", _reader).ConfigureAwait(false);
                        Statistics.LogUnreliableMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOption.Unreliable:
                    {
                        _reader.Position = 1;
                        await InvokeBeforeReceive(_reader).ConfigureAwait(false);
                        await InvokeDataReceived(_reader).ConfigureAwait(false);
                        Statistics.LogUnreliableMessageReceived(_bytes_received);
                        break;
                    }

                // Treat everything else as garbage
                default:
                    {
                        _reader.Recycle();

                        Statistics.LogGarbageMessageReceived(_bytes_received);
                        break;
                    }
            }
        }

        protected override void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                DisposeKeepAliveTimer();
                DisposeReliablePackets();
            }

            base.Dispose(_disposing);
        }
    }
}
