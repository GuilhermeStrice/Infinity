namespace Infinity.Core.Sockets.Native.Win32
{
    [Flags]
    internal enum AddressInfoHints
    {
        AI_PASSIVE = 0x01,
        AI_CANONNAME = 0x02,
        AI_NUMERICHOST = 0x04,
        AI_FQDN = 0x20000,
    }
}
