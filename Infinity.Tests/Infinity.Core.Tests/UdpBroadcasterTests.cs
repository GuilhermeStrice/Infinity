using Infinity.Core.Udp.Broadcast;
using System.Net;
using System.Text;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public class BroadcastTests
    {
        public ITestOutputHelper _output;

        public BroadcastTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void CanStart()
        {
            ManualResetEvent waitHandle = new ManualResetEvent(false);

            byte[] TestData = Encoding.UTF8.GetBytes("pwerowerower");

            byte[] identifier =
            {
                1,
                1,
                1
            };

            using (UdpBroadcastServer server = new UdpBroadcastServer(47777, identifier))
            using (UdpBroadcastClient client = new UdpBroadcastClient(47777, identifier))
            {
                server.Broadcast(TestData);
                Thread.Sleep(1000);

                client.OnBroadcastReceive += (byte[] data, IPEndPoint sender) =>
                {
                    Assert.Equal(TestData, data);

                    waitHandle.Set();
                };

                client.StartListen();

                waitHandle.WaitOne();
            }
        }
    }
}