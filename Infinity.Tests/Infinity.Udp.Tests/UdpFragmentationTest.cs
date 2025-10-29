using Infinity.Tests.Core;
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

        private readonly byte[] _testData = Enumerable.Range(0, 10000).Select(x => (byte)x).ToArray();

        [Fact]
        public async Task FragmentedSendTest()
        {
            Console.WriteLine("FragmentedSendTest");

            ManualResetEvent mutex = new ManualResetEvent(false);

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Configuration.EnableFragmentation = true;
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
                    {
                        var copy = new byte[10000];
                        Array.Copy(data.Message.Buffer, 3, copy, 0, 10000);
                        Assert.Equal(copy, _testData);
                        mutex.Set();

                        data.Recycle();
                    };

                    e.Recycle();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                var writer = UdpMessageFactory.BuildFragmentedMessage();
                writer.Write(_testData);

                await connection.Send(writer);

                writer.Recycle();

                mutex.WaitOne(1000);
            }
        }

        /// <summary>
        /// Checking memory usage
        /// </summary>
        //[Fact]
        public async Task FragmentedSendTest10000()
        {
            Console.WriteLine("FragmentedSendTest10000");

            int count = 0;

            var mutex = new ManualResetEvent(false);

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Configuration.EnableFragmentation = true;
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
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

            int desired_mtu = 1500;

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Configuration.EnableFragmentation = true;
                connection.MaximumAllowedMTU = desired_mtu;
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                Thread.Sleep(5000);

                Assert.True(connection.MTU == desired_mtu);
            }
        }
    }
}
