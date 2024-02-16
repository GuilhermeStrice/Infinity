using Infinity.Core;

namespace Infinity.Udp
{
    public class UdpFragment : IRecyclable
    {
        public int Id { get; set; }
        public MessageReader? Reader { get; set; }

        public UdpFragment()
        {
        }

        public static UdpFragment Get()
        {
            return Pools.FragmentPool.GetObject();
        }

        public void Recycle()
        {
            Reader?.Recycle();
            Id = -1;

            Pools.FragmentPool.PutObject(this);
        }
    }
}
