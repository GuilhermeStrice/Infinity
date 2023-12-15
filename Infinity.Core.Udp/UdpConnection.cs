using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    public abstract partial class UdpConnection : NetworkConnection
    {
        protected static readonly byte[] empty_disconnect_bytes = new byte[1];

        public override float AveragePingMs => ping_ms;
        protected readonly ILogger logger;

        public UdpConnectionStatistics Statistics { get; private set; }

        public UdpConnection(ILogger _logger) : base()
        {
            Statistics = new UdpConnectionStatistics();

            logger = _logger;
        }

        /// <summary>
        ///     Writes the given bytes to the connection.
        /// </summary>
        /// <param name="_bytes">The bytes to write.</param>
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

                if (_writer.Buffer[0] == UdpSendOption.Reliable)
                {
                    // we automagically send fragments if its greater than fragment size
                    if (_writer.Length > (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                    {
                        throw new InfinityException("not allowed");
                    }

                    byte[] buffer = new byte[_writer.Length];
                    Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                    AttachReliableID(buffer, 1);
                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogReliableMessageSent(buffer.Length);
                }
                else if (_writer.Buffer[0] == UdpSendOption.ReliableOrdered)
                {
                    if (_writer.Length > (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                        throw new InfinityException("not allowed");

                    byte[] buffer = new byte[_writer.Length];
                    Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                    OrderedSend(buffer);
                    Statistics.LogReliableMessageSent(buffer.Length);
                }
                else if (_writer.Buffer[0] == UdpSendOption.Fragmented)
                {
                    if (_writer.Length <= (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                        throw new InfinityException("Message not big enough");

                    byte[] buffer = new byte[_writer.Length - 3];
                    Buffer.BlockCopy(_writer.Buffer, 3, buffer, 0, _writer.Length - 3);

                    FragmentedSend(buffer);
                    Statistics.LogFragmentedMessageSent(buffer.Length);
                }
                else
                {
                    byte[] buffer = new byte[_writer.Length];
                    Buffer.BlockCopy(_writer.Buffer, 0, buffer, 0, _writer.Length);

                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogUnreliableMessageSent(buffer.Length);
                }
            }
            catch (Exception e)
            {
                logger?.WriteError("Unknown exception while sending: " + e);
                return SendErrors.Unknown;
            }

            return SendErrors.None;
        }
        
        /// <summary>
        ///     Handles the receiving of data.
        /// </summary>
        /// <param name="_reader">The buffer containing the bytes received.</param>
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

                //Handle acknowledgments
                case UdpSendOptionInternal.Acknowledgement:
                    {
                        AcknowledgementMessageReceive(_reader.Buffer, _bytes_received);
                        Statistics.LogAcknowledgementReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                //We need to acknowledge Handshake and ping messages but dont want to invoke any events!
                case UdpSendOptionInternal.Ping:
                    {
                        ProcessReliableReceive(_reader.Buffer, 1, out id);
                        Statistics.LogPingReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                    // we only receive handshakes at the beggining of the connection in the listener
                case UdpSendOptionInternal.Handshake:
                    {
                        ProcessReliableReceive(_reader.Buffer, 1, out id);
                        Statistics.LogHandshakeReceived(_bytes_received);
                        _reader.Recycle();
                        break;
                    }

                //Handle fragmented messages
                case UdpSendOptionInternal.Fragment:
                    {
                        FragmentMessageReceive(_reader);
                        Statistics.LogFragmentedMessageReceived(_bytes_received);
                        break;
                    }

                case UdpSendOption.Disconnect:
                    {
                        _reader.Offset = 1;
                        _reader.Position = 0;
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

        /// <summary>
        ///     Sends a Handshake packet to the remote endpoint.
        /// </summary>
        /// <param name="_acknowledge_callback">The callback to invoke when the Handshake packet is acknowledged.</param>
        protected void SendHandshake(byte[] _bytes, Action _acknowledge_callback)
        {
            byte[] actual_bytes;
            if (_bytes == null)
            {
                actual_bytes = new byte[1];
            }
            else
            {
                actual_bytes = new byte[_bytes.Length + 1];
                Buffer.BlockCopy(_bytes, 0, actual_bytes, 1, _bytes.Length);
            }

            ReliableSend(UdpSendOptionInternal.Handshake, actual_bytes, _acknowledge_callback);
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
