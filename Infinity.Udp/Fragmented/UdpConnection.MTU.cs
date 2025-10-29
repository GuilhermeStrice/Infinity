using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Infinity.Core;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        public int? ForcedMTU { get; init; }

        public int MinimumMTU
        {
            get
            {
                if (IPMode == IPMode.IPv4)
                {
                    return minimum_mtu_ipv4;
                }

                return minimum_mtu_ipv6;
            }
        }

        public int MaximumAllowedMTU { get; set; } = 2500; // In localhost scenarios MTU can be huge, takes too long to find out. Lets just have a ceiling

        public int MTU { get; private set; } = -1; // we are guaranteed to be able to send at least the minimum IP frame size

        private SemaphoreSlim mtu_lock = new SemaphoreSlim(1, 1);

        private const int minimum_mtu_ipv4 = 576 - 68; // Minimum required by https://datatracker.ietf.org/doc/html/rfc791 - 60 is maximum possible ipv4 header size + 8 bytes for udp header
        private const int minimum_mtu_ipv6 = 1280 - 48; // Minimum required by https://datatracker.ietf.org/doc/html/rfc2460 - 40 is ipv6 header size + 8 bytes for udp header

        private int last_mtu = 0;

        protected async Task BootstrapMTU()
        {
            await mtu_lock.WaitAsync();
            try
            {
                if (MTU == -1)
                {
                    MTU = MinimumMTU;
                }

                if (ForcedMTU != null)
                {
                    MTU = ForcedMTU.Value;
                }
            }
            finally
            {
                mtu_lock.Release();
            }
        }

        protected async Task DiscoverMTU()
        {
            await ExpandMTU();
        }

        protected void FinishMTUExpansion()
        {
            if (MTU == MaximumAllowedMTU)
            {
                return;
            }

            MTU = last_mtu;
        }

        private async Task ExpandMTU()
        {
            await mtu_lock.WaitAsync();
            try
            {
                var buffer = new byte[MTU];

                buffer[0] = UdpSendOptionInternal.TestMTU;

                AttachReliableID(buffer, 1, async () =>
                {
                    await mtu_lock.WaitAsync();
                    try
                    {
                        if (MTU >= MaximumAllowedMTU)
                        {
                            FinishMTUExpansion();
                            return;
                        }

                        last_mtu = MTU;
                        MTU++;
                    }
                    finally
                    {
                        mtu_lock.Release();
                    }

                    await ExpandMTU();
                });

                buffer[MTU - 4] = (byte)MTU;
                buffer[MTU - 3] = (byte)(MTU >> 8);
                buffer[MTU - 2] = (byte)(MTU >> 16);
                buffer[MTU - 1] = (byte)(MTU >> 24);

                await WriteBytesToConnection(buffer, MTU);
            }
            finally
            {
                mtu_lock.Release();
            }
        }

        private async Task MTUTestReceive(MessageReader _reader)
        {
            var result = await ProcessReliableReceive(_reader.Buffer, 1);
            if (result.Item1)
            {
                _reader.Position = _reader.Length - 4;
                int received_mtu = _reader.ReadInt32();

                await mtu_lock.WaitAsync();
                try
                {
                    last_mtu = MTU;
                    MTU = received_mtu;
                }
                finally
                {
                    mtu_lock.Release();
                }
            }
        }
    }
}
