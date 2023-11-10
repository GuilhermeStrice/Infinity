using System.Diagnostics;

namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Class to hold packet data
    /// </summary>
    public class Packet : IRecyclable
    {
        public const int MaxInitialResendDelayMs = 300;
        public const int MinResendDelayMs = 50;
        public const int MaxAdditionalResendDelayMs = 1000;

        public ushort Id;
        private byte[]? Data;
        private readonly UdpConnection Connection;
        private int Length;

        public int NextTimeoutMs;
        public volatile bool Acknowledged;

        public Action? AckCallback;

        public int Retransmissions;
        public Stopwatch Stopwatch = new Stopwatch();

        public Packet(UdpConnection connection)
        {
            Connection = connection;
        }

        public void Set(ushort id, byte[] data, int length, int timeout, Action ackCallback)
        {
            Id = id;
            Data = data;
            Length = length;

            Acknowledged = false;
            NextTimeoutMs = timeout;
            AckCallback = ackCallback;
            Retransmissions = 0;

            Stopwatch.Restart();
        }

        // Packets resent
        public int Resend()
        {
            var connection = Connection;
            if (!Acknowledged && connection != null)
            {
                long lifetimeMs = Stopwatch.ElapsedMilliseconds;
                if (lifetimeMs >= connection.DisconnectTimeoutMs)
                {
                    if (connection.reliableDataPacketsSent.TryRemove(Id, out Packet self))
                    {
                        connection.DisconnectInternal(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={Length}) was not ack'd after {lifetimeMs}ms ({self.Retransmissions} resends)");
                        self.Recycle();
                    }

                    return 0;
                }

                if (lifetimeMs >= NextTimeoutMs)
                {
                    ++Retransmissions;
                    if (connection.ResendLimit != 0
                        && Retransmissions > connection.ResendLimit)
                    {
                        if (connection.reliableDataPacketsSent.TryRemove(Id, out Packet self))
                        {
                            connection.DisconnectInternal(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={Length}) was not ack'd after {self.Retransmissions} resends ({lifetimeMs}ms)");
                            self.Recycle();
                        }

                        return 0;
                    }

                    NextTimeoutMs += (int)Math.Min(NextTimeoutMs * connection.ResendPingMultiplier, MaxAdditionalResendDelayMs);
                    try
                    {
                        connection.WriteBytesToConnection(Data, Length);
                        connection.Statistics.LogMessageResent();
                        return 1;
                    }
                    catch (InvalidOperationException)
                    {
                        connection.DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Could not resend data as connection is no longer connected");
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

            Connection.PacketPool.PutObject(this);
        }
    }
}
