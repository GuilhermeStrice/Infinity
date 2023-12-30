namespace Infinity.Core.Udp
{
    public class UdpConnectionStatistics
    {
        private long bytes_sent = 0;
        private long bytes_received = 0;
        private long packets_sent = 0;
        private long reliable_packets_acknowledged = 0;
        private long acknowledgements_sent = 0;
        private long acknowledgements_received = 0;
        private long pings_sent = 0;
        private long pings_received = 0;
        private long messages_resent = 0;

        private long unreliable_messages_sent = 0;
        private long unreliable_messages_received = 0;

        private long reliable_messages_sent = 0;
        private long reliable_messages_received = 0;

        private long fragmented_messages_sent = 0;
        private long fragmented_messages_received = 0;

        private long handshake_messages_sent = 0;
        private long handshake_messages_received = 0;

        private long garbage_received = 0;

        private long dropped_packets = 0;

        public long BytesSent => Interlocked.Increment(ref bytes_sent);
        public long BytesReceived => Interlocked.Increment(ref bytes_received);
        public long PacksSent => Interlocked.Increment(ref packets_sent);
        public long ReliablePacketsAcknowledged => Interlocked.Read(ref reliable_packets_acknowledged);
        public long AcknowledgementsSent => Interlocked.Read(ref acknowledgements_sent);
        public long AcknowledgementsReceived => Interlocked.Read(ref acknowledgements_received);
        public long PingsSent => Interlocked.Read(ref pings_sent);
        public long PingsReceived => Interlocked.Read(ref pings_received);
        public long MessagesResent => Interlocked.Read(ref messages_resent);

        public long UnreliableMessagesSent => Interlocked.Read(ref unreliable_messages_sent);
        public long UnreliableMessagesReceived => Interlocked.Read(ref unreliable_messages_received);

        public long ReliableMessagesSent => Interlocked.Read(ref reliable_messages_sent);
        public long ReliableMessagesReceived => Interlocked.Read(ref reliable_messages_received);

        public long FragmentedMessagesSent => Interlocked.Read(ref fragmented_messages_sent);
        public long FragmentedMessagesReceived => Interlocked.Read(ref fragmented_messages_received);

        public long HandshakeMessagesSent => Interlocked.Read(ref handshake_messages_sent);
        public long HandshakeMessagesReceived => Interlocked.Read(ref handshake_messages_received);

        public long GarbageReceived => Interlocked.Read(ref garbage_received);

        public long DroppedPackets => Interlocked.Read(ref dropped_packets);

        public long TotalMessagesSent
        {
            get
            {
                return UnreliableMessagesSent +
                    ReliableMessagesSent +
                    FragmentedMessagesSent +
                    AcknowledgementsSent +
                    PingsSent +
                    HandshakeMessagesSent;
            }
        }

        public long TotalMessagesReceived
        {
            get
            {
                return UnreliableMessagesReceived +
                    ReliableMessagesReceived +
                    FragmentedMessagesReceived +
                    AcknowledgementsReceived +
                    PingsReceived +
                    HandshakeMessagesReceived;
            }
        }

        public long PacketLossPercentage
        {
            get
            {
                if (TotalMessagesSent == 0)
                {
                    return 0;
                }

                return DroppedPackets * 100 / TotalMessagesSent;
            }
        }

        public void LogPacketSent(int _length)
        {
            Interlocked.Increment(ref packets_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogAcknowledgementSent(int _length)
        {
            Interlocked.Increment(ref acknowledgements_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogAcknowledgementReceived(int _length)
        {
            Interlocked.Increment(ref acknowledgements_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogReliablePacketAcknowledged()
        {
            Interlocked.Increment(ref reliable_packets_acknowledged);
        }

        public void LogPingSent(int _length)
        {
            Interlocked.Increment(ref pings_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogPingReceived(int _length)
        {
            Interlocked.Increment(ref pings_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogMessageResent(int _length)
        {
            Interlocked.Increment(ref messages_resent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogUnreliableMessageSent(int _length)
        {
            Interlocked.Increment(ref unreliable_messages_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogUnreliableMessageReceived(int _length)
        {
            Interlocked.Increment(ref unreliable_messages_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogReliableMessageSent(int _length)
        {
            Interlocked.Increment(ref reliable_messages_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogReliableMessageReceived(int _length)
        {
            Interlocked.Increment(ref reliable_messages_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogFragmentedMessageSent(int _length)
        {
            Interlocked.Increment(ref fragmented_messages_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogFragmentedMessageReceived(int _length)
        {
            Interlocked.Increment(ref fragmented_messages_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogHandshakeSent(int _length)
        {
            Interlocked.Increment(ref handshake_messages_sent);
            Interlocked.Add(ref bytes_sent, _length);
        }

        public void LogHandshakeReceived(int _length)
        {
            Interlocked.Increment(ref handshake_messages_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogGarbageMessageReceived(int _length)
        {
            Interlocked.Increment(ref garbage_received);
            Interlocked.Add(ref bytes_received, _length);
        }

        public void LogDroppedPacket()
        {
            Interlocked.Increment(ref dropped_packets);
        }
    }
}
