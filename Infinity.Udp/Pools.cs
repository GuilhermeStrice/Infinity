using Infinity.Core;

namespace Infinity.Udp
{
    public static class Pools
    {
        public static ObjectPool<Fragment> FragmentPool = new ObjectPool<Fragment>(() => new Fragment());
        public static ObjectPool<FragmentedMessage> FragmentedMessagePool = new ObjectPool<FragmentedMessage>(() => new FragmentedMessage());
        public static ObjectPool<Packet> PacketPool = new ObjectPool<Packet>(() => new Packet());
    }
}
