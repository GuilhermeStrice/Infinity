namespace Infinity.Udp
{
    internal class UdpSendOptionInternal
    {
        public const byte Handshake = 1;
        public const byte Ping = 2;
        public const byte Acknowledgement = 3;
        public const byte Fragment = 4;
        public const byte AskConfiguration = 6;
        public const byte ShareConfiguration = 7;
    }
}
