namespace Infinity.Core.Udp
{
    internal class UdpSendOptionInternal
    {
        public const byte Handshake = 1;
        public const byte Ping = 2;
        public const byte Acknowledgement = 3;
        public const byte Fragment = 4;
    }

    public class UdpSendOption
    {
        public const byte Unreliable = 10;
        public const byte Reliable = 11;
        public const byte ReliableOrdered = 12;
        public const byte Fragmented = 13;
        public const byte Disconnecting = 14;
        public const byte Disconnect = 15;
    }
}
