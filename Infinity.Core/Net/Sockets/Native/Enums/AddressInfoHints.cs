namespace Infinity.Core.Net.Sockets.Native
{
    [Flags]
    public enum AddressInfoHints
    {
        AI_PASSIVE = 0x01,
        AI_CANONNAME = 0x02,
        AI_NUMERICHOST = 0x04,
        AI_FQDN = 0x20000,
    }
}
