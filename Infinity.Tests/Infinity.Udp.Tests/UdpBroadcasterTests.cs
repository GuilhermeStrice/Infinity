using Infinity.Udp.Broadcast;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Net;
using System.Text;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public class BroadcastTests
    {
        public ITestOutputHelper output;

        public BroadcastTests(ITestOutputHelper _output)
        {
            output = _output;
        }

        // disable for now
        // [Fact]
        public void DoesItWork()
        {
            Console.WriteLine("DoesItWork");

            ManualResetEvent waitHandle = new ManualResetEvent(false);

            byte[] TestData = Encoding.UTF8.GetBytes("pwerowerower");

            byte[] identifier =
            {
                1,
                1,
                1
            };

            using (UdpBroadcaster server = new UdpBroadcaster(47777, identifier))
            using (UdpBroadcastListener client = new UdpBroadcastListener(47777, identifier))
            {
                server.Broadcast(TestData);
                Thread.Sleep(1000);

                client.OnBroadcastReceive += (byte[] data, IPEndPoint sender) =>
                {
                    Assert.Equal(TestData, data);

                    waitHandle.Set();
                };

                client.StartListen();

                waitHandle.WaitOne(5000);
            }
        }
    }
}