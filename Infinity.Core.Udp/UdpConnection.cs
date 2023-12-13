using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    public abstract partial class UdpConnection : NetworkConnection
    {
        internal static readonly byte[] EmptyDisconnectBytes = new byte[1];

        public override float AveragePingMs => _pingMs;
        protected readonly ILogger logger;

        public UdpConnectionStatistics Statistics { get; private set; }

        public UdpConnection(ILogger logger) : base()
        {
            Statistics = new UdpConnectionStatistics();

            this.logger = logger;
            PacketPool = new ObjectPool<Packet>(() => new Packet(this));
        }

        /// <summary>
        ///     Writes the given bytes to the connection.
        /// </summary>
        /// <param name="bytes">The bytes to write.</param>
        public abstract void WriteBytesToConnection(byte[] bytes, int length);

        public override SendErrors Send(MessageWriter msg)
        {
            if (_state != ConnectionState.Connected)
            {
                return SendErrors.Disconnected;
            }

            try
            {
                InvokeBeforeSend(msg);

                byte[] buffer = new byte[msg.Length];
                Buffer.BlockCopy(msg.Buffer, 0, buffer, 0, msg.Length);

                if (msg.SendOption == UdpSendOption.Reliable)
                {
                    // we automagically send fragments if its greater than fragment size
                    if (msg.Length > (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                    {
                        FragmentedSend(buffer);
                        Statistics.LogFragmentedMessageSent(buffer.Length);
                    }
                    else
                    {
                        AttachReliableID(buffer, 1);
                        WriteBytesToConnection(buffer, buffer.Length);
                        Statistics.LogReliableMessageSent(buffer.Length);
                    }
                }
                else if (msg.SendOption == UdpSendOption.ReliableOrdered)
                {
                    if (msg.Length > (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                        throw new InfinityException("not allowed");

                    OrderedSend(buffer);
                    Statistics.LogReliableMessageSent(buffer.Length);
                }
                else
                {
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
        /// <param name="message">The buffer containing the bytes received.</param>
        internal virtual void HandleReceive(MessageReader message, int bytesReceived)
        {
            ushort id;
            switch (message.Buffer[0])
            {
                //Handle reliable receives
                case UdpSendOption.Reliable:
                    {
                        InvokeBeforeReceive(message);
                        ReliableMessageReceive(message);
                        Statistics.LogReliableMessageReceived(bytesReceived);
                        break;
                    }

                case UdpSendOption.ReliableOrdered:
                    {
                        InvokeBeforeReceive(message);
                        OrderedMessageReceived(message);
                        Statistics.LogReliableMessageReceived(bytesReceived);
                        break;
                    }

                //Handle acknowledgments
                case UdpSendOptionInternal.Acknowledgement:
                    {
                        AcknowledgementMessageReceive(message.Buffer, bytesReceived);
                        Statistics.LogAcknowledgementReceived(bytesReceived);
                        message.Recycle();
                        break;
                    }

                //We need to acknowledge Handshake and ping messages but dont want to invoke any events!
                case UdpSendOptionInternal.Ping:
                    {
                        ProcessReliableReceive(message.Buffer, 1, out id);
                        Statistics.LogPingReceived(bytesReceived);
                        message.Recycle();
                        break;
                    }

                    // we only receive handshakes at the beggining of the connection in the listener
                case UdpSendOptionInternal.Handshake:
                    {
                        ProcessReliableReceive(message.Buffer, 1, out id);
                        Statistics.LogHandshakeReceived(bytesReceived);
                        message.Recycle();
                        break;
                    }

                //Handle fragmented messages
                case UdpSendOptionInternal.Fragment:
                    {
                        FragmentMessageReceive(message);
                        Statistics.LogFragmentedMessageReceived(bytesReceived);
                        break;
                    }

                case UdpSendOption.Disconnect:
                    {
                        message.Offset = 1;
                        message.Position = 0;
                        DisconnectRemote("The remote sent a disconnect request", message);
                        Statistics.LogUnreliableMessageReceived(bytesReceived);
                        break;
                    }

                case UdpSendOption.Unreliable:
                    {
                        InvokeBeforeReceive(message);
                        InvokeDataReceived(UdpSendOption.Unreliable, message, 1, bytesReceived);
                        Statistics.LogUnreliableMessageReceived(bytesReceived);
                        break;
                    }

                // Treat everything else as garbage
                default:
                    {
                        message.Recycle();

                        Statistics.LogGarbageMessageReceived(bytesReceived);
                        break;
                    }
            }
        }

        /// <summary>
        ///     Helper method to invoke the data received event.
        /// </summary>
        /// <param name="sendOption">The send option the message was received with.</param>
        /// <param name="buffer">The buffer received.</param>
        /// <param name="dataOffset">The offset of data in the buffer.</param>
        void InvokeDataReceived(byte sendOption, MessageReader buffer, int dataOffset, int bytesReceived)
        {
            buffer.Offset = dataOffset;
            buffer.Length = bytesReceived - dataOffset;
            buffer.Position = 0;

            InvokeDataReceived(buffer, sendOption);
        }

        /// <summary>
        ///     Sends a Handshake packet to the remote endpoint.
        /// </summary>
        /// <param name="acknowledgeCallback">The callback to invoke when the Handshake packet is acknowledged.</param>
        protected void SendHandshake(byte[] bytes, Action acknowledgeCallback)
        {
            byte[] actualBytes;
            if (bytes == null)
            {
                actualBytes = new byte[1];
            }
            else
            {
                actualBytes = new byte[bytes.Length + 1];
                Buffer.BlockCopy(bytes, 0, actualBytes, 1, bytes.Length);
            }

            ReliableSend(UdpSendOptionInternal.Handshake, actualBytes, acknowledgeCallback);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeKeepAliveTimer();
                DisposeReliablePackets();
            }

            base.Dispose(disposing);
        }
    }
}
