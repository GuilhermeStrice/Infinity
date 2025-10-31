using Infinity.Core;
using System.Collections.Concurrent;

namespace Infinity.Udp
{
    /// <summary>
    ///     Holding class for the parts of a fragmented message so far received.
    /// </summary>
    public class UdpFragmentedMessage : IRecyclable
    {
        /// <summary>
        ///     The total number of fragments expected
        /// </summary>
        public int FragmentsCount { get; set; }

        /// <summary>
        ///     The fragments received so far.
        /// </summary>
        public ConcurrentDictionary<int, MessageReader> Fragments { get; set; } = new ConcurrentDictionary<int, MessageReader>();

        public UdpFragmentedMessage()
        {
        }

        public static UdpFragmentedMessage Get()
        {
            return Pools.FragmentedMessagePool.GetObject();
        }

        public void Recycle()
        {
            foreach (var fragment in Fragments.Values)
            {
                fragment.Recycle();
            }

            Fragments.Clear();

            Pools.FragmentedMessagePool.PutObject(this);
        }
    }
}
