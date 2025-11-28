using Infinity.Core;
using Infinity.Tests.Core;
using System.Net;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public class UdpOrderingTests
    {
        ITestOutputHelper? output;

        public UdpOrderingTests(ITestOutputHelper _output)
        {
            output = _output;
        }

        [Fact]
        public async Task OrderedTest()
        {
            Console.WriteLine("OrderedTest");

            int port = Util.GetFreePort();
            var tcs = new TaskCompletionSource();

            int count = 1;
            int lastId = 0;

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            listener.NewConnection += e =>
            {
                e.Connection.DataReceived += data =>
                {
                    data.Message.Position = 3;
                    var receivedId = data.Message.ReadByte();

                    Assert.Equal((byte)lastId, receivedId);

                    Interlocked.Increment(ref lastId);
                    int newCount = Interlocked.Increment(ref count);
                    output.WriteLine(newCount.ToString());

                    data.Recycle();

                    if (newCount == 10)
                    {
                        tcs.SetResult();
                        output.WriteLine("Done");
                    }
                };

                e.Recycle();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);

            for (int i = 0; i < 10; i++)
            {
                var writer = UdpMessageFactory.BuildOrderedMessage();
                writer.Write(20);
                _ = connection.Send(writer);
            }

            await Task.WhenAny(tcs.Task, Task.Delay(5000));
            Assert.True(tcs.Task.IsCompleted, "Did not receive all messages in time.");
        }
    }
}
