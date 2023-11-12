namespace Infinity.Core
{
    /// <summary>
    ///     Holds statistics about the traffic through a <see cref="Connection"/>.
    /// </summary>
    /// <threadsafety static="true" instance="true"/>
    public class ConnectionStatistics
    {
        /// <summary>
        ///     The total number of messages sent.
        /// </summary>
        public int MessagesSent
        {
            get
            {
                return UnreliableMessagesSent +
                    ReliableMessagesSent +
                    FragmentedMessagesSent +
                    AcknowledgementMessagesSent +
                    HandshakeMessagesSent;
            }
        }

        private int streamsSent = 0;
        public int StreamsSent => streamsSent;

        private int streamsReceived = 0;
        public int StreamsReceived => streamsReceived;

        private int packetsSent = 0;
        public int PacketsSent => packetsSent;

        private int reliablePacketsAcknowledged = 0;
        public int ReliablePacketsAcknowledged => reliablePacketsAcknowledged;

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogUnreliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int UnreliableMessagesSent => unreliableMessagesSent;

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        int unreliableMessagesSent = 0;

        /// <summary>
        ///     The number of reliable messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of reliable messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogReliableSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int ReliableMessagesSent => reliableMessagesSent;

        /// <summary>
        ///     The number of unreliable messages sent.
        /// </summary>
        int reliableMessagesSent = 0;

        /// <summary>
        ///     The number of fragmented messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of fragmented messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogFragmentedSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int FragmentedMessagesSent => fragmentedMessagesSent;

        /// <summary>
        ///     The number of fragmented messages sent.
        /// </summary>
        int fragmentedMessagesSent = 0;

        /// <summary>
        ///     The number of acknowledgement messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of acknowledgements that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogAcknowledgementSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int AcknowledgementMessagesSent => acknowledgementMessagesSent;

        /// <summary>
        ///     The number of acknowledgement messages sent.
        /// </summary>
        int acknowledgementMessagesSent = 0;

        /// <summary>
        ///     The number of Handshake messages sent.
        /// </summary>
        /// <remarks>
        ///     This is the number of Handshake messages that were sent from the <see cref="Connection"/>, incremented 
        ///     each time that LogHandshakeSend is called by the Connection. Messages that caused an error are not 
        ///     counted and messages are only counted once all other operations in the send are complete.
        /// </remarks>
        public int HandshakeMessagesSent => handshakeMessagesSent;

        /// <summary>
        ///     The number of Handshake messages sent.
        /// </summary>
        int handshakeMessagesSent = 0;

        /// <summary>
        ///     The number of bytes of data sent.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is the number of bytes of data (i.e. user bytes) that were sent from the <see cref="Connection"/>, 
        ///         accumulated each time that LogSend is called by the Connection. Messages that caused an error are not 
        ///         counted and messages are only counted once all other operations in the send are complete.
        ///     </para>
        ///     <para>
        ///         For the number of bytes including protocol bytes see <see cref="TotalBytesSent"/>.
        ///     </para>
        /// </remarks>
        public long DataBytesSent => Interlocked.Read(ref dataBytesSent);

        /// <summary>
        ///     The number of bytes of data sent.
        /// </summary>
        long dataBytesSent = 0;

        /// <summary>
        ///     The number of bytes sent in total.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is the total number of bytes (the data bytes plus protocol bytes) that were sent from the 
        ///         <see cref="Connection"/>, accumulated each time that LogSend is called by the Connection. Messages that 
        ///         caused an error are not counted and messages are only counted once all other operations in the send are 
        ///         complete.
        ///     </para>
        ///     <para>
        ///         For the number of data bytes excluding protocol bytes see <see cref="DataBytesSent"/>.
        ///     </para>
        /// </remarks>
        public long TotalBytesSent => Interlocked.Read(ref totalBytesSent);

        /// <summary>
        ///     The number of bytes sent in total.
        /// </summary>
        long totalBytesSent = 0;

        /// <summary>
        ///     The total number of messages received.
        /// </summary>
        public int MessagesReceived
        {
            get
            {
                return UnreliableMessagesReceived + 
                    ReliableMessagesReceived + 
                    FragmentedMessagesReceived + 
                    AcknowledgementMessagesReceived +
                    handshakeMessagesReceived;
            }
        }
        
        /// <summary>
        ///     The number of unreliable messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of unreliable messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogUnreliableReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int UnreliableMessagesReceived => unreliableMessagesReceived;

        /// <summary>
        ///     The number of unreliable messages received.
        /// </summary>
        int unreliableMessagesReceived = 0;

        /// <summary>
        ///     The number of reliable messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of reliable messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogReliableReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int ReliableMessagesReceived => reliableMessagesReceived;

        /// <summary>
        ///     The number of reliable messages received.
        /// </summary>
        int reliableMessagesReceived = 0;

        /// <summary>
        ///     The number of fragmented messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of fragmented messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogFragmentedReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int FragmentedMessagesReceived => fragmentedMessagesReceived;

        /// <summary>
        ///     The number of fragmented messages received.
        /// </summary>
        int fragmentedMessagesReceived = 0;

        /// <summary>
        ///     The number of acknowledgement messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of acknowledgement messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogAcknowledgemntReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int AcknowledgementMessagesReceived => acknowledgementMessagesReceived;

        /// <summary>
        ///     The number of acknowledgement messages received.
        /// </summary>
        int acknowledgementMessagesReceived = 0;

        /// <summary>
        ///     The number of ping messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of Handshake messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogHandshakeReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int PingMessagesReceived => pingMessagesReceived;

        /// <summary>
        ///     The number of Handshake messages received.
        /// </summary>
        int pingMessagesReceived = 0;

        /// <summary>
        ///     The number of Handshake messages received.
        /// </summary>
        /// <remarks>
        ///     This is the number of Handshake messages that were received by the <see cref="Connection"/>, incremented
        ///     each time that LogHandshakeReceive is called by the Connection. Messages are counted before the receive event is invoked.
        /// </remarks>
        public int HandshakeMessagesReceived => handshakeMessagesReceived;

        /// <summary>
        ///     The number of Handshake messages received.
        /// </summary>
        int handshakeMessagesReceived = 0;

        /// <summary>
        ///     The number of bytes of data received.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is the number of bytes of data (i.e. user bytes) that were received by the <see cref="Connection"/>, 
        ///         accumulated each time that LogReceive is called by the Connection. Messages are counted before the receive
        ///         event is invoked.
        ///     </para>
        ///     <para>
        ///         For the number of bytes including protocol bytes see <see cref="TotalBytesReceived"/>.
        ///     </para>
        /// </remarks>
        public long DataBytesReceived => Interlocked.Read(ref dataBytesReceived);

        /// <summary>
        ///     The number of bytes of data received.
        /// </summary>
        long dataBytesReceived = 0;

        /// <summary>
        ///     The number of bytes received in total.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         This is the total number of bytes (the data bytes plus protocol bytes) that were received by the 
        ///         <see cref="Connection"/>, accumulated each time that LogReceive is called by the Connection. Messages are 
        ///         counted before the receive event is invoked.
        ///     </para>
        ///     <para>
        ///         For the number of data bytes excluding protocol bytes see <see cref="DataBytesReceived"/>.
        ///     </para>
        /// </remarks>
        public long TotalBytesReceived => Interlocked.Read(ref totalBytesReceived);

        /// <summary>
        ///     The number of bytes received in total.
        /// </summary>
        long totalBytesReceived = 0;

        public int MessagesResent => messagesResent;
        int messagesResent = 0;

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

        /// <param name="totalLength">The total number of bytes sent.</param>
        public void LogPacketSend(int totalLength)
        {
            Interlocked.Increment(ref packetsSent);
            Interlocked.Add(ref totalBytesSent, totalLength);
        }

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
        ///     Logs the sending of a acknowledgement data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes sent.</param>
        /// <remarks>
        ///     This should be called after the data has been sent and should only be called for data that is sent sucessfully.
        /// </remarks>
        public void LogAcknowledgementSend()
        {
            Interlocked.Increment(ref acknowledgementMessagesSent);
        }

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

        /// <summary>
        ///     Logs the receiving of an acknowledgement data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        public void LogAcknowledgementReceive(int totalLength)
        {
            Interlocked.Increment(ref acknowledgementMessagesReceived);
            Interlocked.Add(ref totalBytesReceived, totalLength);
        }

        /// <summary>
        ///     Logs the unique acknowledgement of a ping or reliable data packet.
        /// </summary>
        public void LogReliablePacketAcknowledged()
        {
            Interlocked.Increment(ref reliablePacketsAcknowledged);
        }

        /// <summary>
        ///     Logs the receiving of a Handshake data packet in the statistics.
        /// </summary>
        /// <param name="totalLength">The total number of bytes received.</param>
        /// <remarks>
        ///     This should be called before the received event is invoked so it is up to date for subscribers to that event.
        /// </remarks>
        public void LogPingReceive(int totalLength)
        {
            Interlocked.Increment(ref pingMessagesReceived);
            Interlocked.Add(ref totalBytesReceived, totalLength);
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

        public void LogMessageResent()
        {
            Interlocked.Increment(ref messagesResent);
        }

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
    }
}
