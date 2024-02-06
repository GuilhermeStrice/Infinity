namespace Infinity.Udp
{
    public class UdpConnectionConfiguration
    {
        public ReliableConfiguration Reliability { get; set; } = new ReliableConfiguration();
        public KeepAliveConfiguration KeepAlive { get; set; } = new KeepAliveConfiguration();
        public FragmentedConfiguration Fragmentation { get; set; } = new FragmentedConfiguration();
    }
}
