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
                        await DisconnectInternal(
                            InfinityInternalErrors.PingsWithoutResponse,
                            $"Sent {pings_since_ack} pings that remote has not responded to."
                        ).ConfigureAwait(false);
                        return;
                    }

                    try
                    {
                        await SendPing().ConfigureAwait(false);
                        Interlocked.Increment(ref pings_since_ack);
                    }
                    catch
                    {
                        // optionally log
                    }

                    await Task.Delay(configuration.KeepAliveInterval, ct).ConfigureAwait(false);
                }
            }
            catch (TaskCanceledException)
            {
                // normal cancellation, ignore
            }
            finally
            {
                keep_alive_cts?.Dispose();
                keep_alive_cts = null;
            }
        }

        // Pings are special, quasi-reliable packets. 
        // We send them to trigger responses that validate our connection is alive
        // An unacked ping should never be the sole cause of a disconnect.
        // Rather, the responses will reset our pingsSinceAck, enough unacked 
        // pings should cause a disconnect.
        private async Task SendPing()
        {
            ushort id = (ushort)Interlocked.Increment(ref last_id_allocated);

            var writer = new MessageWriter(allocator);

            writer.Write(UdpSendOptionInternal.Ping);
            writer.Write(id);

            active_pings.AddPing(id);
            await WriteBytesToConnection(writer).ConfigureAwait(false);
            Statistics.LogPingSent(3);
        }
    }
}