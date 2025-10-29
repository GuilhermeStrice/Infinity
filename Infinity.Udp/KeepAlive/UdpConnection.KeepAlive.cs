using Infinity.Core;
using Infinity.Core.KeepAlive;

namespace Infinity.Udp
{   
    public partial class UdpConnection
    {
        private PingBuffer active_pings = new PingBuffer(16);
        private volatile int pings_since_ack = 0;

        private CancellationTokenSource? keep_alive_cts;

        /// <summary>
        /// Starts the async keep-alive loop.
        /// </summary>
        public void InitializeKeepAliveTimer()
        {
            keep_alive_cts = new CancellationTokenSource();
            _ = KeepAliveLoopAsync(keep_alive_cts.Token); // fire-and-forget
        }

        /// <summary>
        /// Stops the async keep-alive loop.
        /// </summary>
        public void DisposeKeepAliveTimer()
        {
            keep_alive_cts?.Cancel();
            keep_alive_cts?.Dispose();
            keep_alive_cts = null;
        }

        private async Task KeepAliveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (State != ConnectionState.Connected)
                        return;

                    if (pings_since_ack >= configuration.MissingPingsUntilDisconnect)
                    {
                        DisposeKeepAliveTimer();
                        DisconnectInternal(
                            InfinityInternalErrors.PingsWithoutResponse,
                            $"Sent {pings_since_ack} pings that remote has not responded to."
                        );
                        return;
                    }

                    try
                    {
                        pings_since_ack++;
                        SendPing();
                    }
                    catch
                    {
                        // optionally log
                    }

                    await Task.Delay(configuration.KeepAliveInterval, ct);
                }
            }
            catch (TaskCanceledException)
            {
                // normal cancellation, ignore
            }
        }

        // Pings are special, quasi-reliable packets. 
        // We send them to trigger responses that validate our connection is alive
        // An unacked ping should never be the sole cause of a disconnect.
        // Rather, the responses will reset our pingsSinceAck, enough unacked 
        // pings should cause a disconnect.
        private void SendPing()
        {
            ushort id = (ushort)++last_id_allocated;

            byte[] bytes = new byte[3];
            bytes[0] = UdpSendOptionInternal.Ping;
            bytes[1] = (byte)(id >> 8);
            bytes[2] = (byte)id;

            active_pings.AddPing(id);
            WriteBytesToConnection(bytes, bytes.Length);
            Statistics.LogPingSent(3);
        }
    }
}