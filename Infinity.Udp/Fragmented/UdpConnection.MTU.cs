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

        private readonly object mtu_lock = new object();

        private const int minimum_mtu_ipv4 = 576 - 68; // Minimum required by https://datatracker.ietf.org/doc/html/rfc791 - 60 is maximum possible ipv4 header size + 8 bytes for udp header
        private const int minimum_mtu_ipv6 = 1280 - 48; // Minimum required by https://datatracker.ietf.org/doc/html/rfc2460 - 40 is ipv6 header size + 8 bytes for udp header

        private int last_mtu = 0;

        protected void BootstrapMTU()
        {
            lock (mtu_lock)
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
        }

        protected void DiscoverMTU()
        {
            ExpandMTU();
        }

        protected void FinishMTUExpansion()
        {
            if (MTU == MaximumAllowedMTU)
            {
                return;
            }

            MTU = last_mtu;
        }

        private void ExpandMTU()
        {
            lock (mtu_lock)
            {
                var buffer = new byte[MTU];

                buffer[0] = UdpSendOptionInternal.TestMTU;

                AttachReliableID(buffer, 1, () =>
                {
                    lock (mtu_lock)
                    {
                        if (MTU >= MaximumAllowedMTU)
                        {
                            FinishMTUExpansion();
                            return;
                        }

                        last_mtu = MTU;
                        MTU++;
                    }

                    ExpandMTU();
                });

                buffer[MTU - 4] = (byte)MTU;
                buffer[MTU - 3] = (byte)(MTU >> 8);
                buffer[MTU - 2] = (byte)(MTU >> 16);
                buffer[MTU - 1] = (byte)(MTU >> 24);

                WriteBytesToConnection(buffer, MTU);
            }
        }

        private void MTUTestReceive(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                _reader.Position = _reader.Length - 4;
                int received_mtu = _reader.ReadInt32();

                lock (mtu_lock)
                {
                    last_mtu = MTU;
                    MTU = received_mtu;
                }
            }
        }
    }
}
