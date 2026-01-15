using System.Threading.Tasks;
using Infinity.Core;
using Infinity.Tests.Core;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public class UdpReliabilityTests
    {
        ITestOutputHelper output;

        public UdpReliabilityTests(ITestOutputHelper _output)
        {
            output = _output;
        }

        [Fact]
        public async Task TestReliableWrapOffByOne()
        {
            Console.WriteLine("TestReliableWrapOffByOne");

            List<MessageReader> messagesReceived = new List<MessageReader>();

            var conn = new NoConnectionUdpConnection(new TestLogger());
            conn.DataReceived += async evt =>
            {
                messagesReceived.Add(evt.Message);
            };

            var data = UdpMessageFactory.BuildReliableMessage(conn);

            Assert.Equal(ushort.MaxValue, conn.ReliableReceiveLast);

            SetReliableId(data, 10);
            await conn.Test_Receive(data);

            // This message may not be received if there is an off-by-one error when marking missed pkts up to 10.
            SetReliableId(data, 9);
            await conn.Test_Receive(data);

            // Both messages should be received.
            Assert.Equal(2, messagesReceived.Count);
            messagesReceived.Clear();

            Assert.Equal(2, conn.BytesSent.Count);
            conn.BytesSent.Clear();

            conn.Dispose();
        }

        [Fact]
        public async Task TestThatAllMessagesAreReceived()
        {
            Console.WriteLine("TestThatAllMessagesAreReceived");

            List<MessageReader> messagesReceived = new List<MessageReader>();

            var conn = new NoConnectionUdpConnection(new TestLogger());
            conn.DataReceived += async evt =>
            {
                messagesReceived.Add(evt.Message);
            };

            var data = UdpMessageFactory.BuildReliableMessage(conn);

            for (int i = 0; i < ushort.MaxValue * 2; ++i)
            {
                // Send a new message, it should be received and ack'd
                SetReliableId(data, i);
                await conn.Test_Receive(data);

                // Resend an old message, it should be ignored
                if (i > 2)
                {
                    SetReliableId(data, i - 1);
                    await conn.Test_Receive(data);

                    // It should still be ack'd
                    Assert.Equal(2, conn.BytesSent.Count);
                    conn.BytesSent.RemoveAt(1);
                }

                Assert.Single(messagesReceived);
                messagesReceived.Clear();

                Assert.Single(conn.BytesSent);
                conn.BytesSent.Clear();
            }

            conn.Dispose();
        }

        [Fact]
        public async Task TestAcksForNotReceivedMessages()
        {
            Console.WriteLine("TestAcksForNotReceivedMessages");

            List<MessageReader> messagesReceived = new List<MessageReader>();

            var conn = new NoConnectionUdpConnection(new TestLogger());
            conn.DataReceived += async evt =>
            {
                messagesReceived.Add(evt.Message);
            };

            var data = UdpMessageFactory.BuildReliableMessage(conn);

            SetReliableId(data, 1);
            await conn.Test_Receive(data);

            SetReliableId(data, 3);
            await conn.Test_Receive(data);

            MessageReader ackPacket = conn.BytesSent[1];
            // Must be ack
            Assert.Equal(4, ackPacket.Length);

            byte recentPackets = ackPacket.Buffer[3];
            var test = recentPackets & 1;
            // Last packet was not received
            Assert.Equal(0, test);
            // The packet before that was.
            Assert.Equal(1, (recentPackets >> 1) & 1);

            conn.Dispose();
        }

        private static void SetReliableId(MessageWriter data, int i)
        {
            ushort id = (ushort)i;
            data.Buffer[1] = (byte)(id >> 8);
            data.Buffer[2] = (byte)id;
        }
    }
}
