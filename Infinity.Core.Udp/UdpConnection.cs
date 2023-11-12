namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Represents a connection that uses the UDP protocol.
    /// </summary>
    public abstract partial class UdpConnection : NetworkConnection
    {
        public static readonly byte[] EmptyDisconnectBytes = new byte[] { UdpSendOption.Disconnect };

        public override float AveragePingMs => _pingMs;
        protected readonly ILogger logger;

        public UdpConnection(ILogger logger) : base()
        {
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
                byte[] buffer = new byte[msg.Length];
                Buffer.BlockCopy(msg.Buffer, 0, buffer, 0, msg.Length);

                if (msg.SendOption == UdpSendOption.Reliable)
                {
                    ResetKeepAliveTimer();

                    if (msg.Length > (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                    {
                        FragmentedSend(buffer);
                        Statistics.LogFragmentedSend(buffer.Length - FragmentHeaderSize);
                    }
                    else
                    {
                        AttachReliableID(buffer, 1);
                        WriteBytesToConnection(buffer, buffer.Length);
                        Statistics.LogReliableSend(buffer.Length - 3);
                    }
                }
                else
                {
                    WriteBytesToConnection(buffer, buffer.Length);
                    Statistics.LogUnreliableSend(buffer.Length - 1);
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
        ///     Handles the reliable/fragmented sending from this connection.
        /// </summary>
        /// <param name="data">The data being sent.</param>
        /// <param name="sendOption">The <see cref="SendOption"/> specified as its byte value.</param>
        /// <param name="ackCallback">The callback to invoke when this packet is acknowledged.</param>
        /// <returns>The bytes that should actually be sent.</returns>
        protected virtual void HandleSend(byte[] data, byte sendOption, Action ackCallback = null)
        {
            if (sendOption == UdpSendOptionInternal.Ping || 
                sendOption == UdpSendOptionInternal.Handshake || 
                sendOption == UdpSendOption.Reliable)
            {
                if (data.Length > (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6))
                {
                    FragmentedSend(data);
                }
                else
                {
                    ReliableSend(sendOption, data, ackCallback);
                }
            }
            else
            {
                UnreliableSend(sendOption, data);
            }
        }

        /// <summary>
        ///     Handles the receiving of data.
        /// </summary>
        /// <param name="message">The buffer containing the bytes received.</param>
        public virtual void HandleReceive(MessageReader message, int bytesReceived)
        {
            ushort id;
            switch (message.Buffer[0])
            {
                //Handle reliable receives
                case UdpSendOption.Reliable:
                    ReliableMessageReceive(message, bytesReceived);
                    break;

                //Handle acknowledgments
                case UdpSendOptionInternal.Acknowledgement:
                    AcknowledgementMessageReceive(message.Buffer, bytesReceived);
                    message.Recycle();
                    break;

                //We need to acknowledge Handshake and ping messages but dont want to invoke any events!
                case UdpSendOptionInternal.Ping:
                    ProcessReliableReceive(message.Buffer, 1, out id);
                    Statistics.LogHandshakeReceive(bytesReceived);
                    message.Recycle();
                    break;
                case UdpSendOptionInternal.Handshake:
                    ProcessReliableReceive(message.Buffer, 1, out id);
                    Statistics.LogHandshakeReceive(bytesReceived);
                    message.Recycle();
                    break;

                //Handle fragmented messages
                case UdpSendOptionInternal.Fragment:
                    FragmentMessageReceive(message);
                    Statistics.LogFragmentedReceive(message.Length, bytesReceived);
                    message.Recycle(); // at this point we dont care about the message
                    break;

                case UdpSendOption.Disconnect:
                    message.Offset = 1;
                    message.Position = 0;
                    DisconnectRemote("The remote sent a disconnect request", message);
                    break;

                case UdpSendOption.Unreliable:
                    InvokeDataReceived(UdpSendOption.Unreliable, message, 1, bytesReceived);
                    Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                    break;

                // Treat everything else as garbage
                default:
                    message.Recycle();

                    // TODO: A new stat for unused data
                    Statistics.LogUnreliableReceive(bytesReceived - 1, bytesReceived);
                    break;
            }
        }

        /// <summary>
        ///     Sends bytes using the unreliable UDP protocol.
        /// </summary>
        /// <param name="sendOption">The SendOption to attach.</param>
        /// <param name="data">The data.</param>
        void UnreliableSend(byte sendOption, byte[] data)
        {
            UnreliableSend(sendOption, data, 0, data.Length);
        }

        /// <summary>
        ///     Sends bytes using the unreliable UDP protocol.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="sendOption">The SendOption to attach.</param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        void UnreliableSend(byte sendOption, byte[] data, int offset, int length)
        {
            byte[] bytes = new byte[length + 1];

            //Add message type
            bytes[0] = sendOption;

            //Copy data into new array
            Buffer.BlockCopy(data, offset, bytes, bytes.Length - length, length);

            //Write to connection
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogUnreliableSend(length);
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

            HandleSend(actualBytes, UdpSendOptionInternal.Handshake, acknowledgeCallback);
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
