namespace Infinity.Core.Net.Sockets
{
    [Flags]
    public enum SocketType : int
    {
        Stream = 1,
        Dgram = 2,
        Raw = 3,
        Rdm = 4,
        Seqpacket = 5,
        Unknown = -1,
    }
}
