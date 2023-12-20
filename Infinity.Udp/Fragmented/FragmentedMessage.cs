namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Holding class for the parts of a fragmented message so far received.
    /// </summary>
    internal class FragmentedMessage : IRecyclable
    {
        /// <summary>
        ///     The total number of fragments expected.
        /// </summary>
        public int FragmentsCount;

        /// <summary>
        ///     The fragments received so far.
        /// </summary>
        public HashSet<Fragment> Fragments = new HashSet<Fragment>();

        public FragmentedMessage()
        {
        }

        public static FragmentedMessage Get()
        {
            return Pools.FragmentedMessagePool.GetObject();
        }

        public void Recycle()
        {
            foreach (var fragment in Fragments)
            {
                fragment.Recycle();
            }

            Fragments.Clear();

            Pools.FragmentedMessagePool.PutObject(this);
        }
    }
}
