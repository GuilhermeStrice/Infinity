using Infinity.Core;
using Infinity.Tests.Core;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public class UdpFragmentationTest
    {
        ITestOutputHelper output;

        public UdpFragmentationTest(ITestOutputHelper output)
        {
            UdpTestHelper._output = output;
            this.output = output;
        }

        private readonly byte[] _testData = Enumerable.Range(0, 60000).Select(x => (byte)x).ToArray();

        [Fact]
        public async Task FragmentedSendTest()
        {
            Console.WriteLine("FragmentedSendTest");

            int port = Util.GetFreePort();
            var tcs = new TaskCompletionSource<bool>();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            listener.Configuration.EnableFragmentation = true;

            listener.NewConnection += e =>
            {
                Console.WriteLine("New connection");
                e.Connection.DataReceived += async data =>
                {
                    var reader = data.Message;
                    reader.Position = 3; // Skip headers
                    var copy = reader.ReadBytes(_testData.Length);

                    Assert.Equal(_testData, copy);

                    data.Recycle();
                    tcs.TrySetResult(true);
                };
                e.Recycle();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);

            await Task.Delay(1000);
            Console.WriteLine(connection.MTU);

            var writer = UdpMessageFactory.BuildFragmentedMessage();
            writer.Write(_testData);

            await connection.Send(writer);

            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(3));
        }

        /// <summary>
        /// Checking memory usage
        /// </summary>
        //[Fact]
        public async Task FragmentedSendTest10000()
        {
            Console.WriteLine("FragmentedSendTest10000");

            int port = Util.GetFreePort();

            int count = 0;

            var mutex = new ManualResetEvent(false);

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port)))
            {
                listener.Configuration.EnableFragmentation = true;
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += async data =>
                    {
                        count++;

                        var messageReader = data.Message;
                        Assert.NotNull(data.Message);

                        var received = new byte[messageReader.Length - 3];
                        Array.Copy(messageReader.Buffer, 3, received, 0, messageReader.Length - 3);

                        Assert.Equal(_testData, received);
                        data.Recycle();

                        if (count == 100)
                            mutex.Set();
                    };

                    e.Recycle();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                Thread.Sleep(100);

                var message = UdpMessageFactory.BuildFragmentedMessage();
                message.Write(_testData, _testData.Length);

                for (int i = 0; i < 100; i++)
                {
                    _ = connection.Send(message);
                    Thread.Sleep(50);
                }
                Thread.Sleep(200);
                message.Recycle();

                mutex.WaitOne(5000);
            }
        }

        [Fact]
        public async Task MTUTest()
        {
            Console.WriteLine("MTUTest");

            int port = Util.GetFreePort();
            int desired_mtu = 1500;

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            listener.Configuration.EnableFragmentation = true;
            connection.MaximumAllowedMTU = desired_mtu;
            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);

            // Wait until MTU reaches desired value or timeout after 5 seconds
            var sw = Stopwatch.StartNew();
            while (connection.MTU != desired_mtu && sw.Elapsed < TimeSpan.FromSeconds(5))
            {
                await Task.Delay(50);
            }

            Assert.Equal(desired_mtu, connection.MTU);
            output.WriteLine(connection.MTU.ToString());
        }
    }
}
