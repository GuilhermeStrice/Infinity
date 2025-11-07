using Infinity.Core;
using System.Threading;

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

        public int MTU
        {
            get
            {
                return mtu;
            }

            private set
            {
                mtu = value;
            }
        } // we are guaranteed to be able to send at least the minimum IP frame size

        private SemaphoreSlim mtu_lock = new SemaphoreSlim(1, 1);

        private const int minimum_mtu_ipv4 = 576 - 68; // Minimum required by https://datatracker.ietf.org/doc/html/rfc791 - 60 is maximum possible ipv4 header size + 8 bytes for udp header
        private const int minimum_mtu_ipv6 = 1280 - 48; // Minimum required by https://datatracker.ietf.org/doc/html/rfc2460 - 40 is ipv6 header size + 8 bytes for udp header

        private int last_mtu = 0;
        private int mtu = -1;

        protected async Task BootstrapMTU()
        {
            await mtu_lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (mtu == -1)
                {
                    Interlocked.Exchange(ref mtu, MinimumMTU);
                }

                if (ForcedMTU != null)
                {
                    Interlocked.Exchange(ref mtu, ForcedMTU.Value);
                }
            }
            finally
            {
                mtu_lock.Release();
            }
        }

        protected async Task DiscoverMTU()
        {
            await ExpandMTU().ConfigureAwait(false);
        }

        protected void FinishMTUExpansion()
        {
            if (mtu == MaximumAllowedMTU)
            {
                return;
            }

            Interlocked.Exchange(ref mtu, last_mtu);
        }

        private async Task ExpandMTU()
        {
            await mtu_lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var buffer = new byte[MTU];

                buffer[0] = UdpSendOptionInternal.TestMTU;

                buffer[mtu - 4] = (byte)mtu;
                buffer[mtu - 3] = (byte)(mtu >> 8);
                buffer[mtu - 2] = (byte)(mtu >> 16);
                buffer[mtu - 1] = (byte)(mtu >> 24);

                var writer = MessageWriter.Get();
                writer.Write(buffer, 0, buffer.Length);

                AttachReliableID(writer, 1, async () =>
                {
                    await mtu_lock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        if (mtu >= MaximumAllowedMTU)
                        {
                            FinishMTUExpansion();
                            return;
                        }

                        Interlocked.Exchange(ref last_mtu, mtu);
                        Interlocked.Increment(ref mtu);
                    }
                    finally
                    {
                        mtu_lock.Release();
                    }

                    await ExpandMTU().ConfigureAwait(false);
                });

                await WriteBytesToConnection(writer).ConfigureAwait(false);
            }
            finally
            {
                mtu_lock.Release();
            }
        }

        private async Task MTUTestReceive(MessageReader _reader)
        {
            var result = await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
            if (result.Item1)
            {
                _reader.Position = _reader.Length - 4;
                int received_mtu = _reader.ReadInt32();

                await mtu_lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    Interlocked.Exchange(ref last_mtu, mtu);
                    Interlocked.Exchange(ref mtu, received_mtu);
                }
                finally
                {
                    mtu_lock.Release();
                }
            }
        }
    }
}
