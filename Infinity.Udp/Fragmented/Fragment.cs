using Infinity.Core;

namespace Infinity.Udp
{
    public class Fragment : IRecyclable
    {
        public int Id { get; set; }
        public MessageReader? Reader { get; set; }

        public Fragment()
        {
        }

        public static Fragment Get()
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
