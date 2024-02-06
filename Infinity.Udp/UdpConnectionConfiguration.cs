namespace Infinity.Udp
{
    public class UdpConnectionConfiguration
    {
        public int ResendTimeoutMs { get; set; } = 0;

        /// <summary>
        /// Max number of times to resend. 0 == no limit
        /// </summary>
        public int ResendLimit { get; set; } = 0;

        /// <summary>
        /// A compounding multiplier to back off resend timeout.
        /// Applied to ping before first timeout when ResendTimeout == 0.
        /// </summary>
        public float ResendPingMultiplier { get; set; } = 2;

        public int DisconnectTimeoutMs { get; set; } = 5000;

        public int KeepAliveInterval { get; set; } = 100;

        public int MissingPingsUntilDisconnect { get; set; } = 6;

        public bool EnableFragmentation { get; set; } = false;

        internal UdpConnectionConfiguration Clone()
        {
            return (UdpConnectionConfiguration)MemberwiseClone();
        }
    }
}
