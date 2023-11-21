using Infinity.Core.Udp.Broadcast;

namespace Infinity.Core.Tests
{
    public class BroadcastTests
    {
        [Fact]
        public void CanStart()
        {
            const string TestData = "pwerowerower";

            using (UdpBroadcastServer caster = new UdpBroadcastServer(47777))
            using (UdpBroadcastClient listener = new UdpBroadcastClient(47777))
            {
                listener.StartListen();

                byte[] identifier =
                {
                    1,
                    1,
                    1
                };

                caster.SetData(identifier, TestData);

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