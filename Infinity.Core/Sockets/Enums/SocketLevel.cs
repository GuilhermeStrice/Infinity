namespace Infinity.Core.Sockets
{
    public enum SocketOptionLevel
    {
        Socket = 0xffff,
        IP = ProtocolType.IP,
        IPv6 = ProtocolType.IPv6,
        Tcp = ProtocolType.Tcp,
        Udp = ProtocolType.Udp,
    }
}
