namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        private PingBuffer active_pings = new PingBuffer(16);

        public int KeepAliveInterval
        {
            get
            {
                return keep_alive_interval;
            }

            set
            {
                keep_alive_interval = value;
                ResetKeepAliveTimer();
            }
        }
        private int keep_alive_interval = 1500;

        public int MissingPingsUntilDisconnect = 6;
        private volatile int pings_since_ack = 0;

        private Timer? keep_alive_timer;

        protected void InitializeKeepAliveTimer()
        {
            keep_alive_timer = new Timer(
                HandleKeepAlive,
                null,
                keep_alive_interval,
                keep_alive_interval
            );
        }

        private void HandleKeepAlive(object? _state)
        {
            if (State != ConnectionState.Connected)
            {
                return;
            }

            if (pings_since_ack >= MissingPingsUntilDisconnect)
            {
                DisposeKeepAliveTimer();
                DisconnectInternal(InfinityInternalErrors.PingsWithoutResponse, 
                    $"Sent {pings_since_ack} pings that remote has not responded to.");
                return;
            }

            try
            {
                pings_since_ack++;
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
            ushort id = (ushort)Interlocked.Increment(ref last_id_allocated);

            byte[] bytes = new byte[3];
            bytes[0] = UdpSendOptionInternal.Ping;
            bytes[1] = (byte)(id >> 8);
            bytes[2] = (byte)id;

            active_pings.AddPing(id);

            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogPingSent(3);
        }

        protected void ResetKeepAliveTimer()
        {
            try
            {
                keep_alive_timer?.Change(keep_alive_interval, keep_alive_interval);
            }
            catch { }
        }

        private void DisposeKeepAliveTimer()
        {
            keep_alive_timer?.Dispose();
        }
    }
}