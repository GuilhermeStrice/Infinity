namespace Infinity.Core.Udp
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
        ///     The fragments received so far.
        /// </summary>
        public HashSet<Fragment> Fragments = new HashSet<Fragment>();

        public FragmentedMessage()
        {
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

            FragmentedMessagePool.PutObject(this);
        }
    }
}
