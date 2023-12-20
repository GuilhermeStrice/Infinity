using System.Diagnostics;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Class to hold packet data
    /// </summary>
    internal class Packet : IRecyclable
    {
        public const int MaxInitialResendDelayMs = 300;
        public const int MinResendDelayMs = 50;
        public const int MaxAdditionalResendDelayMs = 1000;

        public ushort Id;
        private byte[]? Buffer;
        private UdpConnection? Connection;
        private int Length;

        public int NextTimeoutMs;
        public volatile bool Acknowledged;

        public Action? AckCallback;

        public int Retransmissions;
        public Stopwatch Stopwatch = new Stopwatch();

        public void Set(UdpConnection _connection, ushort _id, byte[] _buffer, int _length, int _timeout, Action _ack_callback)
        {
            Connection = _connection;
            Id = _id;
            Buffer = _buffer;
            Length = _length;

            Acknowledged = false;
            NextTimeoutMs = _timeout;
            AckCallback = _ack_callback;
            Retransmissions = 0;

            Stopwatch.Restart();
        }

        // Packets resent
        public int Resend()
        {
            if (!Acknowledged && Connection != null)
            {
                long lifetimeMs = Stopwatch.ElapsedMilliseconds;
                if (lifetimeMs >= Connection.DisconnectTimeoutMs)
                {
                    if (Connection.reliable_data_packets_sent.TryRemove(Id, out Packet self))
                    {
                        Connection.DisconnectInternalPacket(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={Length}) was not ack'd after {lifetimeMs}ms ({self.Retransmissions} resends)");
                        self.Recycle();
                    }

                    return 0;
                }

                if (lifetimeMs >= NextTimeoutMs)
                {
                    // if it's not 0 it means we already sent it once
                    if (Retransmissions != 0)
                    {
                        Connection.Statistics.LogDroppedPacket();
                    }

                    ++Retransmissions;
                    if (Connection.ResendLimit != 0
                        && Retransmissions > Connection.ResendLimit)
                    {
                        if (Connection.reliable_data_packets_sent.TryRemove(Id, out Packet self))
                        {
                            Connection.DisconnectInternalPacket(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={Length}) was not ack'd after {self.Retransmissions} resends ({lifetimeMs}ms)");
                            self.Recycle();
                        }

                        return 0;
                    }

                    NextTimeoutMs += (int)Math.Min(NextTimeoutMs * Connection.ResendPingMultiplier, MaxAdditionalResendDelayMs);

                    try
                    {
                        Connection.WriteBytesToConnection(Buffer, Length);
                        Connection.Statistics.LogMessageResent(Length);

                        return 1;
                    }
                    catch (InvalidOperationException)
                    {
                        Connection.DisconnectInternalPacket(InfinityInternalErrors.ConnectionDisconnected, "Could not resend data as connection is no longer connected");
                    }
                }
            }

            return 0;
        }

        /// <summary>
        ///     Returns this object back to the object pool from whence it came.
        /// </summary>
        public void Recycle()
        {
            Acknowledged = true;

            Pools.PacketPool.PutObject(this);
        }
    }
}
