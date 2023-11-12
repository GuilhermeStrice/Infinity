namespace Infinity.Core.Udp.Fragmented
{
    /// <summary>
    ///     Holding class for the parts of a fragmented message so far received.
    /// </summary>
    internal class FragmentedMessage : IRecyclable
    {
        public static readonly ObjectPool<FragmentedMessage> FragmentedMessagePool = new ObjectPool<FragmentedMessage>(() => new FragmentedMessage());

        /// <summary>
        ///     The total number of fragments expected.
        /// </summary>
        public int FragmentsCount { get; set; }

        /// <summary>
        ///     Since packets above id 65535 reset the counter to 0,
        ///     we need to keep track of this and assign a different id to the fragment,
        ///     otherwise Reconstruct will spill out invalid data
        /// </summary>
        internal volatile int fragmentIdCounter = 0;

        /// <summary>
        ///     The fragments received so far.
        /// </summary>
        public HashSet<Fragment> Fragments { get; } = new HashSet<Fragment>();

        public FragmentedMessage()
        {
        }

        public byte[] Reconstruct()
        {
            if (Fragments.Count != FragmentsCount)
            {
                throw new InfinityException("Can't reconstruct a FragmentedMessage until all fragments are received");
            }

            var buffer = new byte[Fragments.Sum(x => x.Data.Length)];

            var offset = 0;
            foreach (var fragment in Fragments.OrderBy(fragment => fragment.Id))
            {
                var data = fragment.Data;
                Buffer.BlockCopy(data, 0, buffer, offset, data.Length);
                offset += data.Length;
            }

            return buffer;
        }

        public static FragmentedMessage Get()
        {
            return FragmentedMessagePool.GetObject();
        }

        public void Recycle()
        {
            foreach (var fragment in Fragments)
            {
                fragment.Recycle();
            }

            Fragments.Clear();

            Interlocked.Exchange(ref fragmentIdCounter, 0);

            FragmentedMessagePool.PutObject(this);
        }
    }
}
