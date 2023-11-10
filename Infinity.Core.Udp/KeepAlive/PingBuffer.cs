namespace Infinity.Core.Udp
{
    internal class PingBuffer
    {
        private const ushort InvalidatingFactor = ushort.MaxValue / 2;

        private struct PingInfo
        {
            public ushort Id;
            public DateTime SentAt;
        }

        private PingInfo[] activePings;
        private int head; // The location of the next usable activePing

        public PingBuffer(int maxPings)
        {
            activePings = new PingInfo[maxPings];

            // We don't want the first few packets to match id before we set anything.
            for (int i = 0; i < activePings.Length; ++i)
                activePings[i].Id = InvalidatingFactor;
        }

        public void AddPing(ushort id)
        {
            lock (activePings)
            {
                activePings[head].Id = id;
                activePings[head].SentAt = DateTime.UtcNow;

                head++;

                if (head >= activePings.Length)
                    head = 0;
            }
        }

        public bool TryFindPing(ushort id, out DateTime sentAt)
        {
            lock (activePings)
            {
                for (int i = 0; i < activePings.Length; ++i)
                {
                    if (activePings[i].Id == id)
                    {
                        sentAt = activePings[i].SentAt;
                        activePings[i].Id += InvalidatingFactor;
                        return true;
                    }
                }
            }

            sentAt = default;
            return false;
        }
    }
}