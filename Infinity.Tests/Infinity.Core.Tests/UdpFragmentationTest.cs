using Infinity.Core.Udp;
using System.Net;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public class UdpFragmentationTest
    {
        public UdpFragmentationTest(ITestOutputHelper output)
        {
            UdpTestHelper._output = output;
        }

        private readonly byte[] _testData = Enumerable.Range(0, 10000).Select(x => (byte)x).ToArray();

        [Fact]
        public void FragmentedSendTest()
        {
            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                TaskCompletionSource<bool> result = new TaskCompletionSource<bool>();

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

                        result.SetResult(true);
                    };
                };

                listener.Start();
                connection.Connect();

                var message = MessageWriter.Get(3);
                message.Buffer[0] = UdpSendOption.Reliable;
                message.Write(_testData, _testData.Length);

                connection.Send(message);

                result.Task.Wait();
            }
        }

        /// <summary>
        /// Checking memory usage
        /// </summary>
        [Fact]
        public void FragmentedSendTest10000()
        {
            int count = 0;

            TaskCompletionSource<bool> result = new TaskCompletionSource<bool>();

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
                            result.SetResult(true);
                    };
                };

                listener.Start();
                connection.Connect();

                Thread.Sleep(100);

                var message = MessageWriter.Get(3);
                message.Buffer[0] = UdpSendOption.Reliable;
                message.Write(_testData, _testData.Length);

                for (int i = 0; i < 100; i++)
                {
                    connection.Send(message);
                    Thread.Sleep(5);
                }

                message.Recycle();
            }

            result.Task.Wait();
        }
    }
}
