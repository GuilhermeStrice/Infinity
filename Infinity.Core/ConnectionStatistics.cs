namespace Infinity.Core
{
    public class ConnectionStatistics
    {
        #region Common

            public long DataBytesSent => Interlocked.Read(ref dataBytesSent);
            long dataBytesSent = 0;

            public long DataBytesReceived => Interlocked.Read(ref dataBytesReceived);
            long dataBytesReceived = 0;

            public long TotalBytesSent => Interlocked.Read(ref totalBytesSent);
            long totalBytesSent = 0;

            public long TotalBytesReceived => Interlocked.Read(ref totalBytesReceived);
            long totalBytesReceived = 0;

        #endregion

        #region Tcp

        public int StreamsSent => streamsSent;
        private int streamsSent = 0;

        public int StreamsReceived => streamsReceived;
        private int streamsReceived = 0;

        public void LogStreamSent(int length, int fullLength)
        {
            Interlocked.Increment(ref streamsSent);
            Interlocked.Add(ref totalBytesSent, fullLength);
            Interlocked.Add(ref dataBytesReceived, length);
        }

        public void LogStreamReceived(int length, int fullLength)
        {
            Interlocked.Increment(ref streamsReceived);
            Interlocked.Add(ref totalBytesReceived, fullLength);
            Interlocked.Add(ref dataBytesSent, length);
        }

        #endregion

        #region Udp

        public int MessagesSent
            {
                get
                {
                    return unreliableMessagesSent +
                        reliableMessagesSent +
                        fragmentedMessagesSent +
                        acknowledgementMessagesSent +
                        HandshakeMessagesSent;
                }
            }

            public int MessagesReceived
            {
                get
                {
                    return unreliableMessagesReceived +
                        reliableMessagesReceived +
                        fragmentedMessagesReceived +
                        acknowledgementMessagesReceived +
                        handshakeMessagesReceived;
                }
            }

            #region Internal

                public int PacketsSent => packetsSent;
                private int packetsSent = 0;

                public int ReliablePacketsAcknowledged => reliablePacketsAcknowledged;
                private int reliablePacketsAcknowledged = 0;

                public int AcknowledgementMessagesSent => acknowledgementMessagesSent;
                int acknowledgementMessagesSent = 0;

                public int AcknowledgementMessagesReceived => acknowledgementMessagesReceived;
                int acknowledgementMessagesReceived = 0;

                public int PingMessagesReceived => pingMessagesReceived;
                int pingMessagesReceived = 0;

                public int MessagesResent => messagesResent;
                int messagesResent = 0;

                public void LogPacketSend(int totalLength)
                {
                    Interlocked.Increment(ref packetsSent);
                    Interlocked.Add(ref totalBytesSent, totalLength);
                }

                public void LogAcknowledgementSend()
                {
                    Interlocked.Increment(ref acknowledgementMessagesSent);
                }

                public void LogAcknowledgementReceive(int totalLength)
                {
                    Interlocked.Increment(ref acknowledgementMessagesReceived);
                    Interlocked.Add(ref totalBytesReceived, totalLength);
                }

                public void LogReliablePacketAcknowledged()
                {
                    Interlocked.Increment(ref reliablePacketsAcknowledged);
                }

                public void LogPingReceive(int totalLength)
                {
                    Interlocked.Increment(ref pingMessagesReceived);
                    Interlocked.Add(ref totalBytesReceived, totalLength);
                }

                public void LogMessageResent()
                {
                    Interlocked.Increment(ref messagesResent);
                }

            #endregion

            #region Unreliable
                public int UnreliableMessagesSent => unreliableMessagesSent;
                int unreliableMessagesSent = 0;

                public int UnreliableMessagesReceived => unreliableMessagesReceived;
                int unreliableMessagesReceived = 0;

                /// <summary>
                ///     Logs the sending of an unreliable data packet in the statistics.
                /// </summary>
                /// <param name="dataLength">The number of bytes of data sent.</param>
                /// <remarks>
                ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
                /// </remarks>
                public void LogUnreliableSend(int dataLength)
                {
                    Interlocked.Increment(ref unreliableMessagesSent);
                    Interlocked.Add(ref dataBytesSent, dataLength);
                }

                /// <summary>
                ///     Logs the receiving of an unreliable data packet in the statistics.
                /// </summary>
                /// <param name="dataLength">The number of bytes of data received.</param>
                /// <param name="totalLength">The total number of bytes received.</param>
                /// <remarks>
                ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
                /// </remarks>
                public void LogUnreliableReceive(int dataLength, int totalLength)
                {
                    Interlocked.Increment(ref unreliableMessagesReceived);
                    Interlocked.Add(ref dataBytesReceived, dataLength);
                    Interlocked.Add(ref totalBytesReceived, totalLength);
                }

            #endregion

            #region Reliable

                public int ReliableMessagesSent => reliableMessagesSent;
                int reliableMessagesSent = 0;

                public int ReliableMessagesReceived => reliableMessagesReceived;
                int reliableMessagesReceived = 0;

                /// <summary>
                ///     Logs the sending of a reliable data packet in the statistics.
                /// </summary>
                /// <param name="dataLength">The number of bytes of data sent.</param>
                /// <remarks>
                ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
                /// </remarks>
                public void LogReliableSend(int dataLength)
                {
                    Interlocked.Increment(ref reliableMessagesSent);
                    Interlocked.Add(ref dataBytesSent, dataLength);
                }

                /// <summary>
                ///     Logs the receiving of a reliable data packet in the statistics.
                /// </summary>
                /// <param name="dataLength">The number of bytes of data received.</param>
                /// <param name="totalLength">The total number of bytes received.</param>
                /// <remarks>
                ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
                /// </remarks>
                public void LogReliableReceive(int dataLength, int totalLength)
                {
                    Interlocked.Increment(ref reliableMessagesReceived);
                    Interlocked.Add(ref dataBytesReceived, dataLength);
                    Interlocked.Add(ref totalBytesReceived, totalLength);
                }

            #endregion

            #region Fragmented

                public int FragmentedMessagesSent => fragmentedMessagesSent;
                int fragmentedMessagesSent = 0;

                public int FragmentedMessagesReceived => fragmentedMessagesReceived;
                int fragmentedMessagesReceived = 0;

                /// <summary>
                ///     Logs the sending of a fragmented data packet in the statistics.
                /// </summary>
                /// <param name="dataLength">The number of bytes of data sent.</param>
                /// <param name="totalLength">The total number of bytes sent.</param>
                /// <remarks>
                ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
                /// </remarks>
                public void LogFragmentedSend(int dataLength)
                {
                    Interlocked.Increment(ref fragmentedMessagesSent);
                    Interlocked.Add(ref dataBytesSent, dataLength);
                }

                /// <summary>
                ///     Logs the receiving of a fragmented data packet in the statistics.
                /// </summary>
                /// <param name="dataLength">The number of bytes of data received.</param>
                /// <param name="totalLength">The total number of bytes received.</param>
                /// <remarks>
                ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
                /// </remarks>
                public void LogFragmentedReceive(int dataLength, int totalLength)
                {
                    Interlocked.Increment(ref fragmentedMessagesReceived);
                    Interlocked.Add(ref dataBytesReceived, dataLength);
                    Interlocked.Add(ref totalBytesReceived, totalLength);
                }

            #endregion

            #region Handshake

                public int HandshakeMessagesSent => handshakeMessagesSent;
                int handshakeMessagesSent = 0;

                public int HandshakeMessagesReceived => handshakeMessagesReceived;
                int handshakeMessagesReceived = 0;

                /// <summary>
                ///     Logs the sending of a hellp data packet in the statistics.
                /// </summary>
                /// <param name="totalLength">The total number of bytes sent.</param>
                /// <remarks>
                ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
                /// </remarks>
                public void LogHandshakeSend()
                {
                    Interlocked.Increment(ref handshakeMessagesSent);
                }

                /// <summary>
                ///     Logs the receiving of a Handshake data packet in the statistics.
                /// </summary>
                /// <param name="totalLength">The total number of bytes received.</param>
                /// <remarks>
                ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
                /// </remarks>
                public void LogHandshakeReceive(int totalLength)
                {
                    Interlocked.Increment(ref handshakeMessagesReceived);
                    Interlocked.Add(ref totalBytesReceived, totalLength);
                }

            #endregion
        #endregion
    }
}
