namespace Infinity.Core.Udp
{
    internal class Fragment : IRecyclable
    {
        public static readonly ObjectPool<Fragment> FragmentPool = new ObjectPool<Fragment>(() => new Fragment());

        public int Id { get; set; }
        public MessageReader Data { get; set; }

        public Fragment()
        {
        }

        public static Fragment Get()
        {
            return FragmentPool.GetObject();
        }

        public void Recycle()
        {
            Data.Recycle();
            Id = -1;

            FragmentPool.PutObject(this);
        }
    }
}
