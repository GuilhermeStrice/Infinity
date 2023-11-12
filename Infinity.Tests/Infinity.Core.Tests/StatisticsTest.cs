namespace Infinity.Core.Tests
{
    public class StatisticsTests
    {
        [Fact]
        public void SendTests()
        {
            ConnectionStatistics statistics = new ConnectionStatistics();

            statistics.LogUnreliableSend(10);

            Assert.Equal(1, statistics.MessagesSent);
            Assert.Equal(1, statistics.UnreliableMessagesSent);
            Assert.Equal(0, statistics.ReliableMessagesSent);
            Assert.Equal(0, statistics.FragmentedMessagesSent);
            Assert.Equal(0, statistics.AcknowledgementMessagesSent);
            Assert.Equal(0, statistics.HandshakeMessagesSent);

            Assert.Equal(10, statistics.DataBytesSent);

            statistics.LogReliableSend(5);

            Assert.Equal(2, statistics.MessagesSent);
            Assert.Equal(1, statistics.UnreliableMessagesSent);
            Assert.Equal(1, statistics.ReliableMessagesSent);
            Assert.Equal(0, statistics.FragmentedMessagesSent);
            Assert.Equal(0, statistics.AcknowledgementMessagesSent);
            Assert.Equal(0, statistics.HandshakeMessagesSent);

            Assert.Equal(15, statistics.DataBytesSent);

            statistics.LogFragmentedSend(6);

            Assert.Equal(3, statistics.MessagesSent);
            Assert.Equal(1, statistics.UnreliableMessagesSent);
            Assert.Equal(1, statistics.ReliableMessagesSent);
            Assert.Equal(1, statistics.FragmentedMessagesSent);
            Assert.Equal(0, statistics.AcknowledgementMessagesSent);
            Assert.Equal(0, statistics.HandshakeMessagesSent);

            Assert.Equal(21, statistics.DataBytesSent);

            statistics.LogAcknowledgementSend();

            Assert.Equal(4, statistics.MessagesSent);
            Assert.Equal(1, statistics.UnreliableMessagesSent);
            Assert.Equal(1, statistics.ReliableMessagesSent);
            Assert.Equal(1, statistics.FragmentedMessagesSent);
            Assert.Equal(1, statistics.AcknowledgementMessagesSent);
            Assert.Equal(0, statistics.HandshakeMessagesSent);

            Assert.Equal(21, statistics.DataBytesSent);

            statistics.LogHandshakeSend();

            Assert.Equal(5, statistics.MessagesSent);
            Assert.Equal(1, statistics.UnreliableMessagesSent);
            Assert.Equal(1, statistics.ReliableMessagesSent);
            Assert.Equal(1, statistics.FragmentedMessagesSent);
            Assert.Equal(1, statistics.AcknowledgementMessagesSent);
            Assert.Equal(1, statistics.HandshakeMessagesSent);

            Assert.Equal(21, statistics.DataBytesSent);

            Assert.Equal(0, statistics.MessagesReceived);
            Assert.Equal(0, statistics.UnreliableMessagesReceived);
            Assert.Equal(0, statistics.ReliableMessagesReceived);
            Assert.Equal(0, statistics.FragmentedMessagesReceived);
            Assert.Equal(0, statistics.AcknowledgementMessagesReceived);
            Assert.Equal(0, statistics.HandshakeMessagesReceived);

            Assert.Equal(0, statistics.DataBytesReceived);
            Assert.Equal(0, statistics.TotalBytesReceived);

            statistics.LogPacketSend(11);
            Assert.Equal(11, statistics.TotalBytesSent);
        }

        [Fact]
        public void ReceiveTests()
        {
            ConnectionStatistics statistics = new ConnectionStatistics();

            statistics.LogUnreliableReceive(10, 11);

            Assert.Equal(1, statistics.MessagesReceived);
            Assert.Equal(1, statistics.UnreliableMessagesReceived);
            Assert.Equal(0, statistics.ReliableMessagesReceived);
            Assert.Equal(0, statistics.FragmentedMessagesReceived);
            Assert.Equal(0, statistics.AcknowledgementMessagesReceived);
            Assert.Equal(0, statistics.HandshakeMessagesReceived);

            Assert.Equal(10, statistics.DataBytesReceived);
            Assert.Equal(11, statistics.TotalBytesReceived);

            statistics.LogReliableReceive(5, 8);

            Assert.Equal(2, statistics.MessagesReceived);
            Assert.Equal(1, statistics.UnreliableMessagesReceived);
            Assert.Equal(1, statistics.ReliableMessagesReceived);
            Assert.Equal(0, statistics.FragmentedMessagesReceived);
            Assert.Equal(0, statistics.AcknowledgementMessagesReceived);
            Assert.Equal(0, statistics.HandshakeMessagesReceived);

            Assert.Equal(15, statistics.DataBytesReceived);
            Assert.Equal(19, statistics.TotalBytesReceived);

            statistics.LogFragmentedReceive(6, 10);

            Assert.Equal(3, statistics.MessagesReceived);
            Assert.Equal(1, statistics.UnreliableMessagesReceived);
            Assert.Equal(1, statistics.ReliableMessagesReceived);
            Assert.Equal(1, statistics.FragmentedMessagesReceived);
            Assert.Equal(0, statistics.AcknowledgementMessagesReceived);
            Assert.Equal(0, statistics.HandshakeMessagesReceived);

            Assert.Equal(21, statistics.DataBytesReceived);
            Assert.Equal(29, statistics.TotalBytesReceived);

            statistics.LogAcknowledgementReceive(4);

            Assert.Equal(4, statistics.MessagesReceived);
            Assert.Equal(1, statistics.UnreliableMessagesReceived);
            Assert.Equal(1, statistics.ReliableMessagesReceived);
            Assert.Equal(1, statistics.FragmentedMessagesReceived);
            Assert.Equal(1, statistics.AcknowledgementMessagesReceived);
            Assert.Equal(0, statistics.HandshakeMessagesReceived);

            Assert.Equal(21, statistics.DataBytesReceived);
            Assert.Equal(33, statistics.TotalBytesReceived);

            statistics.LogHandshakeReceive(7);

            Assert.Equal(5, statistics.MessagesReceived);
            Assert.Equal(1, statistics.UnreliableMessagesReceived);
            Assert.Equal(1, statistics.ReliableMessagesReceived);
            Assert.Equal(1, statistics.FragmentedMessagesReceived);
            Assert.Equal(1, statistics.AcknowledgementMessagesReceived);
            Assert.Equal(1, statistics.HandshakeMessagesReceived);

            Assert.Equal(21, statistics.DataBytesReceived);
            Assert.Equal(40, statistics.TotalBytesReceived);

            Assert.Equal(0, statistics.MessagesSent);
            Assert.Equal(0, statistics.UnreliableMessagesSent);
            Assert.Equal(0, statistics.ReliableMessagesSent);
            Assert.Equal(0, statistics.FragmentedMessagesSent);
            Assert.Equal(0, statistics.AcknowledgementMessagesSent);
            Assert.Equal(0, statistics.HandshakeMessagesSent);

            Assert.Equal(0, statistics.DataBytesSent);
            Assert.Equal(0, statistics.TotalBytesSent);
        }
    }
}