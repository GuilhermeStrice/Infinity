namespace Infinity.Udp
{
    internal class PingBuffer
    {
        private const ushort InvalidatingFactor = ushort.MaxValue / 2;

        private PingInfo[] active_pings;
        private int head; // The location of the next usable activePing

        public PingBuffer(int _max_pings)
        {
            active_pings = new PingInfo[_max_pings];

            // We don't want the first few packets to match id before we set anything.
            for (int i = 0; i < active_pings.Length; ++i)
            {
                active_pings[i].Id = InvalidatingFactor;
            }
        }

        public void AddPing(ushort _id)
        {
            lock (active_pings)
            {
                active_pings[head].Id = _id;
                active_pings[head].SentAt = DateTime.UtcNow;

                head++;

                if (head >= active_pings.Length)
                {
                    head = 0;
                }
            }
        }

        public bool TryFindPing(ushort _id, out DateTime _sent_at)
        {
            lock (active_pings)
            {
                for (int i = 0; i < active_pings.Length; ++i)
                {
                    if (active_pings[i].Id == _id)
                    {
                        _sent_at = active_pings[i].SentAt;
                        active_pings[i].Id += InvalidatingFactor;
                        return true;
                    }
                }
            }

            _sent_at = default;
            return false;
        }
    }
}