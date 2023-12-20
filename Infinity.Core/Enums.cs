namespace Infinity.Core
{
    public enum IPMode
    {
        IPv4,
        IPv6
    }

    public enum ConnectionState
    {
        NotConnected,
        Connecting,
        Connected,
    }

    public enum SendErrors
    {
        None,
        Disconnected,
        Unknown
    }

    public enum InfinityInternalErrors
    {
        SocketExceptionSend,
        SocketExceptionReceive,
        ReceivedZeroBytes,
        PingsWithoutResponse,
        ReliablePacketWithoutResponse,
        ConnectionDisconnected
    }

    public enum Protocol : byte
    {
        Udp,
        Tcp,
    }
}
