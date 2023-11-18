using Infinity.Core.Udp.Broadcast;

namespace Infinity.Core.Tests
{
    public class BroadcastTests
    {
        [Fact]
        public void CanStart()
        {
            const string TestData = "pwerowerower";

            using (UdpBroadcaster caster = new UdpBroadcaster(47777))
            using (UdpBroadcastListener listener = new UdpBroadcastListener(47777))
            {
                listener.StartListen();

                caster.SetData(TestData);

                caster.Broadcast();
                Thread.Sleep(1000);

                var pkt = listener.GetPackets();
                foreach (var p in pkt)
                {
                    Console.WriteLine($"{p.Data} {p.Sender}");
                    Assert.Equal(TestData, p.Data);
                }

                Assert.True(pkt.Length >= 1);
            }
        }
    }
}