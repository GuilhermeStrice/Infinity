namespace Infinity.Udp
{
    public class UdpConnectionStatistics
    {
        private ulong bytes_sent = 0;
        private ulong bytes_received = 0;
        private ulong packets_sent = 0;
        private ulong reliable_packets_acknowledged = 0;
        private ulong acknowledgements_sent = 0;
        private ulong acknowledgements_received = 0;
        private ulong pings_sent = 0;
        private ulong pings_received = 0;
        private ulong messages_resent = 0;

        private ulong unreliable_messages_sent = 0;
        private ulong unreliable_messages_received = 0;

        private ulong reliable_messages_sent = 0;
        private ulong reliable_messages_received = 0;

        private ulong fragmented_messages_sent = 0;
        private ulong fragmented_messages_received = 0;

        private ulong mtu_test_messages_sent = 0;
        private ulong mtu_test_messages_received = 0;

        private ulong handshake_messages_sent = 0;
        private ulong handshake_messages_received = 0;

        private ulong garbage_received = 0;

        private ulong dropped_packets = 0;

        public ulong BytesSent => Interlocked.Read(ref bytes_sent);
        public ulong BytesReceived => Interlocked.Read(ref bytes_received);
        public ulong PacksSent => Interlocked.Read(ref packets_sent);
        public ulong ReliablePacketsAcknowledged => Interlocked.Read(ref reliable_packets_acknowledged);
        public ulong AcknowledgementsSent => Interlocked.Read(ref acknowledgements_sent);
        public ulong AcknowledgementsReceived => Interlocked.Read(ref acknowledgements_received);
        public ulong PingsSent => Interlocked.Read(ref pings_sent);
        public ulong PingsReceived => Interlocked.Read(ref pings_received);
        public ulong MessagesResent => Interlocked.Read(ref messages_resent);

        public ulong UnreliableMessagesSent => Interlocked.Read(ref unreliable_messages_sent);
        public ulong UnreliableMessagesReceived => Interlocked.Read(ref unreliable_messages_received);

        public ulong ReliableMessagesSent => Interlocked.Read(ref reliable_messages_sent);
        public ulong ReliableMessagesReceived => Interlocked.Read(ref reliable_messages_received);

        public ulong FragmentedMessagesSent => Interlocked.Read(ref fragmented_messages_sent);
        public ulong FragmentedMessagesReceived => Interlocked.Read(ref fragmented_messages_received);

        public ulong MTUTestMessagesSent => Interlocked.Read(ref mtu_test_messages_sent);
        public ulong MTUTestMessagesReceived => Interlocked.Read(ref mtu_test_messages_received);

        public ulong HandshakeMessagesSent => Interlocked.Read(ref handshake_messages_sent);
        public ulong HandshakeMessagesReceived => Interlocked.Read(ref handshake_messages_received);

        public ulong GarbageReceived => Interlocked.Read(ref garbage_received);

        public ulong DroppedPackets => Interlocked.Read(ref dropped_packets);

        public ulong TotalMessagesSent
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

        public ulong TotalMessagesReceived
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

        public ulong PacketLossPercentage
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
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogAcknowledgementSent(int _length)
        {
            Interlocked.Increment(ref acknowledgements_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogAcknowledgementReceived(int _length)
        {
            Interlocked.Increment(ref acknowledgements_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogReliablePacketAcknowledged()
        {
            Interlocked.Increment(ref reliable_packets_acknowledged);
        }

        public void LogPingSent(int _length)
        {
            Interlocked.Increment(ref pings_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogPingReceived(int _length)
        {
            Interlocked.Increment(ref pings_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogMessageResent(int _length)
        {
            Interlocked.Increment(ref messages_resent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogUnreliableMessageSent(int _length)
        {
            Interlocked.Increment(ref unreliable_messages_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogUnreliableMessageReceived(int _length)
        {
            Interlocked.Increment(ref unreliable_messages_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogReliableMessageSent(int _length)
        {
            Interlocked.Increment(ref reliable_messages_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogReliableMessageReceived(int _length)
        {
            Interlocked.Increment(ref reliable_messages_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogFragmentedMessageSent(int _length)
        {
            Interlocked.Increment(ref fragmented_messages_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogFragmentedMessageReceived(int _length)
        {
            Interlocked.Increment(ref fragmented_messages_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogMTUTestMessageSent(int _length)
        {
            Interlocked.Increment(ref mtu_test_messages_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogMTUTestMessageReceived(int _length)
        {
            Interlocked.Increment(ref mtu_test_messages_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogHandshakeSent(int _length)
        {
            Interlocked.Increment(ref handshake_messages_sent);
            Interlocked.Add(ref bytes_sent, (ulong)_length);
        }

        public void LogHandshakeReceived(int _length)
        {
            Interlocked.Increment(ref handshake_messages_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogGarbageMessageReceived(int _length)
        {
            Interlocked.Increment(ref garbage_received);
            Interlocked.Add(ref bytes_received, (ulong)_length);
        }

        public void LogDroppedPacket()
        {
            Interlocked.Increment(ref dropped_packets);
        }
    }
}