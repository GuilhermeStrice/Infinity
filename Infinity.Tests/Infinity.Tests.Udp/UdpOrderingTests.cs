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

                        Assert.Equal(20, receivedData);

                        Interlocked.Increment(ref count);

                        if (count == 200)
                            result.SetResult(true);
                    };
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);

                Thread.Sleep(100);

                var writer = UdpMessageFactory.BuildOrderedMessage();
                writer.Write(20);

                // needs further testing
                for (int i = 0; i < 200; i++)
                {
                    connection.Send(writer);
                    Thread.Sleep(1); // might be a local host problem, but if packets are sent too quickly this test doesn't run
                }

                writer.Recycle();
            }

            result.Task.Wait();
        }
    }
}
