namespace Infinity.Core.Udp
{
    public class UdpConnectionStatistics
    {
        long bytesSent = 0;
        long bytesReceived = 0;
        long packetsSent = 0;
        long reliablePacketsAcknowledged = 0;
        long acknowledgementsSent = 0;
        long acknowledgementsReceived = 0;
        long pingsSent = 0;
        long pingsReceived = 0;
        long messagesResent = 0;

        long unreliableMessagesSent = 0;
        long unreliableMessagesReceived = 0;

        long reliableMessagesSent = 0;
        long reliableMessagesReceived = 0;

        long fragmentedMessagesSent = 0;
        long fragmentedMessagesReceived = 0;

        long handshakeMessagesSent = 0;
        long handshakeMessagesReceived = 0;

        long garbageReceived = 0;

        public long BytesSent => Interlocked.Increment(ref bytesSent);
        public long BytesReceived => Interlocked.Increment(ref bytesReceived);
        public long PacksSent => Interlocked.Increment(ref packetsSent);
        public long ReliablePacketsAcknowledged => Interlocked.Read(ref reliablePacketsAcknowledged);
        public long AcknowledgementsSent => Interlocked.Read(ref acknowledgementsSent);
        public long AcknowledgementsReceived => Interlocked.Read(ref acknowledgementsReceived);
        public long PingsSent => Interlocked.Read(ref pingsSent);
        public long PingsReceived => Interlocked.Read(ref pingsReceived);
        public long MessagesResent => Interlocked.Read(ref messagesResent);

        public long UnreliableMessagesSent => Interlocked.Read(ref unreliableMessagesSent);
        public long UnreliableMessagesReceived => Interlocked.Read(ref unreliableMessagesReceived);

        public long ReliableMessagesSent => Interlocked.Read(ref reliableMessagesSent);
        public long ReliableMessagesReceived => Interlocked.Read(ref reliableMessagesReceived);

        public long FragmentedMessagesSent => Interlocked.Read(ref fragmentedMessagesSent);
        public long FragmentedMessagesReceived => Interlocked.Read(ref fragmentedMessagesReceived);

        public long HandshakeMessagesSent => Interlocked.Read(ref handshakeMessagesSent);
        public long HandshakeMessagesReceived => Interlocked.Read(ref handshakeMessagesReceived);

        public long GarbageReceived => Interlocked.Read(ref garbageReceived);

        public long TotalMessagesSent
        {
            get
            {
                return unreliableMessagesSent +
                    reliableMessagesSent +
                    fragmentedMessagesSent +
                    acknowledgementsSent +
                    handshakeMessagesSent;
            }
        }

        public long TotalMessagesReceived
        {
            get
            {
                return unreliableMessagesReceived +
                    reliableMessagesReceived +
                    fragmentedMessagesReceived +
                    acknowledgementsReceived +
                    pingsReceived +
                    handshakeMessagesReceived;
            }
        }

        public void LogPacketSent(int length)
        {
            Interlocked.Increment(ref packetsSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogAcknowledgementSent(int length)
        {
            Interlocked.Increment(ref acknowledgementsSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogAcknowledgementReceived(int length)
        {
            Interlocked.Increment(ref acknowledgementsReceived);
            Interlocked.Add(ref bytesReceived, length);
        }

        public void LogReliablePacketAcknowledged()
        {
            Interlocked.Increment(ref reliablePacketsAcknowledged);
        }

        public void LogPingSent(int length)
        {
            Interlocked.Increment(ref pingsSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogPingReceived(int length)
        {
            Interlocked.Increment(ref pingsReceived);
            Interlocked.Add(ref bytesReceived, length);
        }

        public void LogMessageResent(int length)
        {
            Interlocked.Increment(ref messagesResent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogUnreliableMessageSent(int length)
        {
            Interlocked.Increment(ref unreliableMessagesSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogUnreliableMessageReceived(int length)
        {
            Interlocked.Increment(ref unreliableMessagesReceived);
            Interlocked.Add(ref bytesReceived, length);
        }

        public void LogReliableMessageSent(int length)
        {
            Interlocked.Increment(ref reliableMessagesSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogReliableMessageReceived(int length)
        {
            Interlocked.Increment(ref reliableMessagesReceived);
            Interlocked.Add(ref bytesReceived, length);
        }

        public void LogFragmentedMessageSent(int length)
        {
            Interlocked.Increment(ref fragmentedMessagesSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogFragmentedMessageReceived(int length)
        {
            Interlocked.Increment(ref fragmentedMessagesReceived);
            Interlocked.Add(ref bytesReceived, length);
        }

        public void LogHandshakeSent(int length)
        {
            Interlocked.Increment(ref handshakeMessagesSent);
            Interlocked.Add(ref bytesSent, length);
        }

        public void LogHandshakeReceived(int length)
        {
            Interlocked.Increment(ref handshakeMessagesReceived);
            Interlocked.Add(ref bytesReceived, length);
        }

        public void LogUnformattedMessageReceived(int length)
        {
            Interlocked.Increment(ref garbageReceived);
            Interlocked.Add(ref bytesReceived, length);
        }
    }
}
