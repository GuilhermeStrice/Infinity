namespace Infinity.Udp
{
    public class UdpSendOption
    {
        public const byte Unreliable = 100;
        public const byte Reliable = 101;
        public const byte ReliableOrdered = 102;
        public const byte Fragmented = 103;
        public const byte Disconnect = 104;
    }
}
