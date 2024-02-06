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

        protected Action<MessageReader> OnReceiveConfiguration;

        internal UdpConnectionConfiguration configuration = new UdpConnectionConfiguration(); // new - Only used for bootstrapping

        public UdpConnection(ILogger _logger) : base()
        {
            logger = _logger;
        }

        public abstract void WriteBytesToConnection(byte[] _bytes, int _length);

        public override SendErrors Send(MessageWriter _writer)
        {
            if (state != ConnectionState.Connected)
            {
                return SendErrors.Disconnected;
            }

            try
            {
                InvokeBeforeSend(_writer);

                switch (_writer.Buffer[0])
                {
                    case UdpSendOption.Reliable:
                        {
                            if (_writer.Length > MTU)
                            {
                                throw new InfinityException("not allowed");
                            }

                            byte[] buffer = new byte[_writer.Length];
                            Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                            ReliableSend(buffer);
                            break;
                        }
                    case UdpSendOption.ReliableOrdered:
                        {
                            if (_writer.Length > MTU)
                            {
                                throw new InfinityException("not allowed");
                            }

                            byte[] buffer = new byte[_writer.Length];
                            Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                            OrderedSend(buffer);
                            Statistics.LogReliableMessageSent(buffer.Length);

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

                                byte[] buffer = new byte[_writer.Length - 3];
                                Buffer.BlockCopy(_writer.Buffer, 3, buffer, 0, _writer.Length - 3);

                                FragmentedSend(buffer);
                                Statistics.LogFragmentedMessageSent(buffer.Length);
                            }
                            else
                            {
                                throw new InfinityException("Enable fragmentation to use fragmented messages");
                            }

                            break;
                        }
                    default: // applies to disconnect and unreliable
                        {
                            byte[] buffer = new byte[_writer.Length];
                            Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                            WriteBytesToConnection(buffer, buffer.Length);
                            Statistics.LogUnreliableMessageSent(buffer.Length);

                            break;
                        }
                }
            }
            catch (InfinityException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                logger?.WriteError("Unknown exception while sending: " + e);
                return SendErrors.Unknown;
            }

            return SendErrors.None;
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

        protected internal virtual void HandleReceive(MessageReader _reader, int _bytes_received)
        {
            ushort id;
            switch (_reader.Buffer[0])
            {
                //Handle reliable receives
                case UdpSendOption.Reliable:
                    {
                        InvokeBeforeReceive(_reader);
                        ReliableMessageReceive(_reader);
                        Statistics.LogReliableMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOption.ReliableOrdered:
                    {
                        InvokeBeforeReceive(_reader);
                        OrderedMessageReceived(_reader);
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
                        ProcessReliableReceive(_reader.Buffer, 1, out id);
                        Statistics.LogPingReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOptionInternal.Handshake:
                    {
                        ProcessReliableReceive(_reader.Buffer, 1, out id);
                        Statistics.LogHandshakeReceived(_bytes_received);
                        break;
                    }

                case UdpSendOptionInternal.Fragment:
                    {
                        FragmentMessageReceive(_reader);
                        Statistics.LogFragmentedMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOptionInternal.TestMTU:
                    {
                        MTUTestReceive(_reader);
                        Statistics.LogMTUTestMessageReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOptionInternal.AskConfiguration:
                    {
                        ProcessReliableReceive(_reader.Buffer, 1, out id);
                        ShareConfiguration();
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOptionInternal.ShareConfiguration:
                    {
                        ProcessReliableReceive(_reader.Buffer, 1, out id);
                        OnReceiveConfiguration?.Invoke(_reader);
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOption.Disconnect:
                    {
                        _reader.Position = 1;
                        DisconnectRemote("The remote sent a disconnect request", _reader);
                        Statistics.LogUnreliableMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOption.Unreliable:
                    {
                        InvokeBeforeReceive(_reader);
                        InvokeDataReceived(_reader);
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

        private void ShareConfiguration()
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

            ReliableSend(buffer);
        }
    }
}
