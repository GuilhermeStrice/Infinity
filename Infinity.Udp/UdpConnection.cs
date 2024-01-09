namespace Infinity.Core.Udp
{
    public abstract partial class UdpConnection : NetworkConnection
    {
        public int BufferSize
        {
            get
            {
                return IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6;
            }
        }

        public override float AveragePingMs => ping_ms;

        public UdpConnectionStatistics Statistics { get; private set; }

        protected static readonly byte[] empty_disconnect_bytes = new byte[1] { UdpSendOption.Disconnect };
        protected readonly ILogger logger;

        public UdpConnection(ILogger _logger) : base()
        {
            Statistics = new UdpConnectionStatistics();

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
                            if (_writer.Length > BufferSize)
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
                            if (_writer.Length > BufferSize)
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
                            if (_writer.Length <= BufferSize)
                            {
                                throw new InfinityException("Message not big enough");
                            }

                            byte[] buffer = new byte[_writer.Length - 3];
                            Buffer.BlockCopy(_writer.Buffer, 3, buffer, 0, _writer.Length - 3);

                            FragmentedSend(buffer);
                            Statistics.LogFragmentedMessageSent(buffer.Length);

                            break;
                        }
                    default:
                        {
                            byte[] buffer = new byte[_writer.Length];
                            Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                            WriteBytesToConnection(buffer, buffer.Length);
                            Statistics.LogUnreliableMessageSent(buffer.Length);

                            break;
                        }
                }
            }
            catch (Exception e)
            {
                logger?.WriteError("Unknown exception while sending: " + e);
                return SendErrors.Unknown;
            }

            return SendErrors.None;
        }

        protected void SendHandshake(MessageWriter _writer, Action _acknowledge_callback)
        {
            byte[] buffer = new byte[_writer.Length];
            Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

            ReliableSend(buffer, _acknowledge_callback);
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

        internal virtual void HandleReceive(MessageReader _reader, int _bytes_received)
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
                        _reader.Recycle();
                        break;
                    }

                case UdpSendOptionInternal.Fragment:
                    {
                        FragmentMessageReceive(_reader);
                        Statistics.LogFragmentedMessageReceived(_bytes_received);
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
    }
}
