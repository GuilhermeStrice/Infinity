namespace Infinity.Core.Udp
{
    internal class UdpSendOptionInternal
    {
        public const byte Handshake = 8;
        public const byte Ping = 12;
        public const byte Acknowledgement = 10;
        public const byte Fragment = 11;
    }

    public class UdpSendOption
    {
        public const byte Unreliable = 0;
        public const byte Reliable = 1;
        public const byte ReliableOrdered = 20;
        public const byte Fragmented = 21;
        public const byte Disconnect = 9;
    }
}
