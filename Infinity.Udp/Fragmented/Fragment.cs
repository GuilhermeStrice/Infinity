namespace Infinity.Core.Udp
{
    public class Fragment : IRecyclable
    {
        public int Id;
        public MessageReader? Reader;

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
