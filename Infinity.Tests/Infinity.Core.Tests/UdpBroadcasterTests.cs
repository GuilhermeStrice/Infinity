using Infinity.Core.Udp.Broadcast;
using System.Net;
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

            const string TestData = "pwerowerower";

            byte[] identifier =
            {
                1,
                1,
                1
            };

            UdpBroadcastServer server = new UdpBroadcastServer(47777, identifier);
            UdpBroadcastClient client = new UdpBroadcastClient(47777, identifier);

            server.Broadcast(TestData);
            Thread.Sleep(1000);

            client.OnBroadcastReceive += (string data, IPEndPoint sender) =>
            {
                _output.WriteLine(data);
                _output.WriteLine("----- from -----");
                _output.WriteLine(sender.ToString());
                Assert.Equal(TestData, data);

                waitHandle.Set();
            };

            client.StartListen();

            waitHandle.WaitOne();
            Console.ReadKey();
        }
    }
}