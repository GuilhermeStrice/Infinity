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

        volatile int count = 1;
        volatile byte lastId = 0;

        [Fact]
        public async Task OrderedTest()
        {
            Console.WriteLine("OrderedTest");

            ManualResetEvent mutex = new ManualResetEvent(false);

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
                    {
                        data.Message.Position = 3;

                        var receivedId = data.Message.ReadByte();

                        Assert.Equal(lastId, receivedId);

                        ++lastId;

                        count++;

                        data.Recycle();

                        if (count == 300)
                        {
                            mutex.Set();
                            output.WriteLine("Done");
                        }
                    };

                    e.Recycle();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                Thread.Sleep(100);

                var writer = UdpMessageFactory.BuildOrderedMessage();
                writer.Write(20);

                // needs further testing
                for (int i = 0; i < 300; i++)
                {
                    await connection.Send(writer);
                }

                writer.Recycle();

                mutex.WaitOne(5000);
            }
        }
    }
}
