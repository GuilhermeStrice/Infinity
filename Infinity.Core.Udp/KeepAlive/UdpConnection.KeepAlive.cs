namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        private PingBuffer activePings = new PingBuffer(16);

        /// <summary>
        ///     The interval from data being received or transmitted to a keepalive packet being sent in milliseconds.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Keepalive packets serve to close connections when an endpoint abruptly disconnects and to ensure than any
        ///         NAT devices do not close their translation for our argument. By ensuring there is regular contact the
        ///         connection can detect and prevent these issues.
        ///     </para>
        ///     <para>
        ///         The default value is 10 seconds, set to System.Threading.Timeout.Infinite to disable keepalive packets.
        ///     </para>
        /// </remarks>
        public int KeepAliveInterval
        {
            get
            {
                return keepAliveInterval;
            }

            set
            {
                keepAliveInterval = value;
                ResetKeepAliveTimer();
            }
        }
        private int keepAliveInterval = 1500;

        public int MissingPingsUntilDisconnect { get; set; } = 6;
        private volatile int pingsSinceAck = 0;

        /// <summary>
        ///     The timer creating keepalive pulses.
        /// </summary>
        private Timer keepAliveTimer;

        /// <summary>
        ///     Starts the keepalive timer.
        /// </summary>
        protected void InitializeKeepAliveTimer()
        {
            keepAliveTimer = new Timer(
                HandleKeepAlive,
                null,
                keepAliveInterval,
                keepAliveInterval
            );
        }

        private void HandleKeepAlive(object ?state)
        {
            if (State != ConnectionState.Connected)
            {
                return;
            }

            if (pingsSinceAck >= MissingPingsUntilDisconnect)
            {
                DisposeKeepAliveTimer();
                DisconnectInternal(InfinityInternalErrors.PingsWithoutResponse, 
                    $"Sent {pingsSinceAck} pings that remote has not responded to.");
                return;
            }

            try
            {
                pingsSinceAck++;
                SendPing();
            }
            catch
            {
            }
        }

        // Pings are special, quasi-reliable packets. 
        // We send them to trigger responses that validate our connection is alive
        // An unacked ping should never be the sole cause of a disconnect.
        // Rather, the responses will reset our pingsSinceAck, enough unacked 
        // pings should cause a disconnect.
        private void SendPing()
        {
            ushort id = (ushort)Interlocked.Increment(ref lastIDAllocated);

            byte[] bytes = new byte[3];
            bytes[0] = UdpSendOptionInternal.Ping;
            bytes[1] = (byte)(id >> 8);
            bytes[2] = (byte)id;

            activePings.AddPing(id);

            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogPingSent(3);
        }

        /// <summary>
        ///     Resets the keepalive timer to zero.
        /// </summary>
        protected void ResetKeepAliveTimer()
        {
            try
            {
                keepAliveTimer?.Change(keepAliveInterval, keepAliveInterval);
            }
            catch { }
        }

        /// <summary>
        ///     Disposes of the keep alive timer.
        /// </summary>
        private void DisposeKeepAliveTimer()
        {
            keepAliveTimer?.Dispose();
        }
    }
}