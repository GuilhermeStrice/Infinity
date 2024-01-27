using Infinity.Core;
using System.Diagnostics;

namespace Infinity.Udp
{
    public class Packet : IRecyclable
    {
        public const int MaxInitialResendDelayMs = 300;
        public const int MinResendDelayMs = 50;
        public const int MaxAdditionalResendDelayMs = 1000;

        public ushort Id { get; private set; }

        public int NextTimeoutMs { get; private set; }
        public volatile bool Acknowledged;

        public Action? AckCallback;

        public int Retransmissions { get; private set; }
        public Stopwatch Stopwatch = new Stopwatch();

        private byte[]? buffer;
        private UdpConnection? connection;

        public void Set(UdpConnection _connection, ushort _id, byte[] _buffer, int _timeout, Action _ack_callback)
        {
            connection = _connection;
            Id = _id;
            buffer = _buffer;

            Acknowledged = false;
            NextTimeoutMs = _timeout;
            AckCallback = _ack_callback;
            Retransmissions = 0;

            Stopwatch.Restart();
        }

        public int Resend()
        {
            if (!Acknowledged && connection != null)
            {
                long lifetimeMs = Stopwatch.ElapsedMilliseconds;
                if (lifetimeMs >= connection.DisconnectTimeoutMs)
                {
                    if (connection.reliable_data_packets_sent.TryRemove(Id, out Packet self))
                    {
                        connection.DisconnectInternalPacket(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={buffer.Length}) was not ack'd after {lifetimeMs}ms ({self.Retransmissions} resends)");
                        self.Recycle();
                    }

                    return 0;
                }

                if (lifetimeMs >= NextTimeoutMs)
                {
                    // if it's not 0 it means we already sent it once
                    if (Retransmissions != 0)
                    {
                        connection.Statistics.LogDroppedPacket();
                    }

                    ++Retransmissions;
                    if (connection.ResendLimit != 0
                        && Retransmissions > connection.ResendLimit)
                    {
                        if (connection.reliable_data_packets_sent.TryRemove(Id, out Packet self))
                        {
                            connection.DisconnectInternalPacket(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {self.Id} (size={buffer.Length}) was not ack'd after {self.Retransmissions} resends ({lifetimeMs}ms)");
                            self.Recycle();
                        }

                        return 0;
                    }

                    NextTimeoutMs += (int)Math.Min(NextTimeoutMs * connection.ResendPingMultiplier, MaxAdditionalResendDelayMs);

                    try
                    {
                        connection.WriteBytesToConnection(buffer, buffer.Length);
                        connection.Statistics.LogMessageResent(buffer.Length);

                        return 1;
                    }
                    catch (InvalidOperationException)
                    {
                        connection.DisconnectInternalPacket(InfinityInternalErrors.ConnectionDisconnected, "Could not resend data as connection is no longer connected");
                    }
                }
            }

            return 0;
        }

        public void Recycle()
        {
            Acknowledged = true;

            Pools.PacketPool.PutObject(this);
        }
    }
}
