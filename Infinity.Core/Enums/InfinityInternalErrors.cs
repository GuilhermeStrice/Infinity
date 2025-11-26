namespace Infinity.Core
{
    public enum InfinityInternalErrors : byte
    {
        SocketExceptionSend,
        SocketExceptionReceive,
        ReceivedZeroBytes,
        PingsWithoutResponse,
        ReliablePacketWithoutResponse,
        ConnectionDisconnected
    }
}
