using Infinity.Core.Udp;
using System.Net;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public class UdpOrderingTests
    {
        public UdpOrderingTests(ITestOutputHelper output)
        {
            UdpTestHelper._output = output;
        }

        [Fact]
        public void OrderedTest()
        {
            int count = 1;
            int lastId = 1;

            TaskCompletionSource<bool> result = new TaskCompletionSource<bool>();

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
                    {
                        data.Message.Position = 3;

                        var receivedId = (data.Message.ReadByte() + 1) % 255;

                        Assert.Equal(lastId, receivedId);

                        lastId = (lastId + 1) % 255;

                        var receivedData = data.Message.ReadInt32();

                        Assert.Equal(receivedData, 20);

                        Interlocked.Increment(ref count);

                        if (count == 300)
                            result.SetResult(true);
                    };
                };

                listener.Start();
                connection.Connect();

                Thread.Sleep(100);

                var message = MessageWriter.Get();
                message.Write(UdpSendOption.ReliableOrdered);
                message.Position += 2;
                message.Write(20);

                // needs further testing
                for (int i = 0; i < 300; i++)
                {
                    connection.Send(message);
                    Thread.Sleep(1); // might be a local host problem, but if packets are sent too quickly this test doesn't run
                }

                message.Recycle();
            }

            result.Task.Wait();
        }
    }
}
