﻿using Infinity.Core;
using System.Diagnostics;

namespace Infinity.Udp
{
    public class UdpPacket : IRecyclable
    {
        public const int MaxInitialResendDelayMs = 300;
        public const int MinResendDelayMs = 50;
        public const int MaxAdditionalResendDelayMs = 1000;

        public int NextTimeoutMs { get; private set; }
        public volatile bool Acknowledged;

        public Action? AckCallback;

        public int Retransmissions
        {
            get
            {
                return retransmissions;
            }

            private set
            {
                retransmissions = value;
            }
        }

        public Stopwatch Stopwatch = new Stopwatch();

        private byte[]? buffer;
        private int retransmissions;
        private UdpConnection? connection;

        public void Set(UdpConnection _connection, byte[] _buffer, int _timeout, Action _ack_callback)
        {
            connection = _connection;
            buffer = _buffer;

            Acknowledged = false;
            NextTimeoutMs = _timeout;
            AckCallback = _ack_callback;
            retransmissions = 0;

            Stopwatch.Restart();
        }

        public async Task<int> Resend()
        {
            if (!Acknowledged && connection != null)
            {
                ushort id = (ushort)((buffer[1] << 8) + buffer[2]);

                long lifetimeMs = Stopwatch.ElapsedMilliseconds;
                if (lifetimeMs >= connection.configuration.DisconnectTimeoutMs)
                {
                    if (connection.reliable_data_packets_sent.TryRemove(id, out UdpPacket self))
                    {
                        connection.DisconnectInternalPacket(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {id} (size={buffer.Length}) was not ack'd after {lifetimeMs}ms ({self.Retransmissions} resends)");
                        self.Recycle();
                    }

                    return 0;
                }

                if (lifetimeMs >= NextTimeoutMs)
                {
                    // if it's not 0 it means we already sent it once
                    if (retransmissions != 0)
                    {
                        connection.Statistics.LogDroppedPacket();
                    }

                    retransmissions = (int)Interlocked.Increment(ref retransmissions);
                    if (connection.configuration.ResendLimit != 0
                        && retransmissions > connection.configuration.ResendLimit)
                    {
                        if (connection.reliable_data_packets_sent.TryRemove(id, out UdpPacket self))
                        {
                            connection.DisconnectInternalPacket(InfinityInternalErrors.ReliablePacketWithoutResponse, $"Reliable packet {id} (size={buffer.Length}) was not ack'd after {self.Retransmissions} resends ({lifetimeMs}ms)");
                            self.Recycle();
                        }

                        return 0;
                    }

                    NextTimeoutMs += (int)Math.Min(
                        NextTimeoutMs * connection.configuration.ResendPingMultiplier,
                        MaxAdditionalResendDelayMs
                    );

                    // Ensure we don't exceed disconnect timeout
                    NextTimeoutMs = Math.Min(NextTimeoutMs, connection.configuration.DisconnectTimeoutMs);

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
