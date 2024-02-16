using Infinity.Core;

namespace Infinity.Udp
{
    public static class Pools
    {
        public static ObjectPool<UdpFragment> FragmentPool = new ObjectPool<UdpFragment>(() => new UdpFragment());
        public static ObjectPool<UdpFragmentedMessage> FragmentedMessagePool = new ObjectPool<UdpFragmentedMessage>(() => new UdpFragmentedMessage());
        public static ObjectPool<UdpPacket> PacketPool = new ObjectPool<UdpPacket>(() => new UdpPacket());
    }
}
