using Infinity.Core.Tests;
using Infinity.Core.Udp;
using System.Net;
using Xunit.Abstractions;

namespace Infinity.Tests.Udp
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
        public void FragmentedSendTest()
        {
            ManualResetEvent mutex = new ManualResetEvent(false);

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
                    {
                        var messageReader = data.Message;
                        Assert.NotNull(data.Message);

                        var received = new byte[messageReader.Length - 3];
                        Array.Copy(messageReader.Buffer, 3, received, 0, messageReader.Length - 3);

                        Assert.Equal(_testData, received);
                        data.Message.Recycle();

                        output.WriteLine("yes");

                        mutex.Set();
                    };
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);

                var writer = UdpMessageFactory.BuildFragmentedMessage();
                writer.Write(_testData);

                connection.Send(writer);

                mutex.WaitOne();
            }
        }

        /// <summary>
        /// Checking memory usage
        /// </summary>
        //[Fact]
        public void FragmentedSendTest10000()
        {
            int count = 0;

            var mutex = new ManualResetEvent(false);

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.NewConnection += e =>
                {
                    e.Connection.DataReceived += data =>
                    {
                        Interlocked.Increment(ref count);

                        var messageReader = data.Message;
                        Assert.NotNull(data.Message);

                        var received = new byte[messageReader.Length - 3];
                        Array.Copy(messageReader.Buffer, 3, received, 0, messageReader.Length - 3);

                        Assert.Equal(_testData, received);
                        data.Message.Recycle();

                        if (count == 100)
                            mutex.Set();
                    };
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);

                Thread.Sleep(100);

                var message = UdpMessageFactory.BuildFragmentedMessage();
                message.Write(_testData, _testData.Length);

                for (int i = 0; i < 100; i++)
                {
                    connection.Send(message);
                    Thread.Sleep(50);
                }
                Thread.Sleep(200);
                message.Recycle();

                mutex.WaitOne();
            }
        }
    }
}
