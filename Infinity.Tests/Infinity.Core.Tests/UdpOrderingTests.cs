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

            TaskCompletionSource<bool> result = new TaskCompletionSource<bool>();

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
                    {
                        data.Message.Position = 3;

                        var receivedId = data.Message.ReadByte() + 1;

                        data.Message.Position++;

                        Assert.Equal(count, receivedId);

                        var hey = data.Message.ReadInt32();

                        var expected = 20;
                        Assert.Equal(hey, expected);

                        Interlocked.Increment(ref count);

                        if (count == 50)
                            result.SetResult(true);
                    };
                };

                listener.Start();
                connection.Connect();

                Thread.Sleep(100);

                var message = MessageWriter.Get(UdpSendOption.ReliableOrdered);
                message.Write(20);

                for (int i = 0; i < 50; i++)
                {
                    connection.Send(message);
                    Thread.Sleep(10);
                }

                message.Recycle();
            }

            result.Task.Wait();
        }
    }
}
