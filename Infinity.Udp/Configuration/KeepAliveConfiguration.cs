namespace Infinity.Udp
{
    public class KeepAliveConfiguration
    {
        public int KeepAliveInterval { get; set; } = 100;

        public int MissingPingsUntilDisconnect { get; set; } = 6;
    }
}
