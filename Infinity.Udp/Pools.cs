namespace Infinity.Core.Udp
{
    internal static class Pools
    {
        public static readonly ObjectPool<Fragment> FragmentPool = new ObjectPool<Fragment>(() => new Fragment());
        public static readonly ObjectPool<FragmentedMessage> FragmentedMessagePool = new ObjectPool<FragmentedMessage>(() => new FragmentedMessage());
        public static readonly ObjectPool<Packet> PacketPool = new ObjectPool<Packet>(() => new Packet());
    }
}
