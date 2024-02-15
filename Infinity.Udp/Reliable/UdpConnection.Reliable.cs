using Infinity.Core;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        internal FastConcurrentDictionary<ushort, Packet> reliable_data_packets_sent = new FastConcurrentDictionary<ushort, Packet>();

        private int last_id_allocated = -1;

        public int ManageReliablePackets()
        {
            int output = 0;
            if (reliable_data_packets_sent.Count > 0)
            {
                reliable_data_packets_sent.ForEach(id_packet =>
                {
                    Packet packet = id_packet.Value;

                    try
                    {
                        output += packet.Resend();
                    }
                    catch { }
                });
            }

            return output;
        }

        protected void ReliableSend(byte[] _buffer, Action _ack_callback = null)
        {
            //Inform keepalive not to send for a while
            KeepAliveTimerWait();

            AttachReliableID(_buffer, 1, _ack_callback);
            WriteBytesToConnection(_buffer, _buffer.Length);
            Statistics.LogReliableMessageSent(_buffer.Length);
        }

        private void DisposeReliablePackets()
        {
            reliable_data_packets_sent.ForEach(id_packet =>
            {
                ushort id = id_packet.Key;
                if (reliable_data_packets_sent.TryRemove(id, out var packet))
                {
                    packet.Recycle();
                }
            });
        }

        protected void AttachReliableID(byte[] _buffer, int _offset, Action _ack_callback = null)
        {
            ushort id = (ushort)++last_id_allocated;

            _buffer[_offset] = (byte)(id >> 8);
            _buffer[_offset + 1] = (byte)id;

            int resend_delay_ms = configuration.ResendTimeoutMs;
            if (resend_delay_ms <= 0)
            {
                resend_delay_ms = Math.Clamp((int)(AveragePingMs * configuration.ResendPingMultiplier), 
                    Packet.MinResendDelayMs, Packet.MaxInitialResendDelayMs);
            }

            Packet packet = Pools.PacketPool.GetObject();
            packet.Set(
                this,
                _buffer,
                resend_delay_ms,
                _ack_callback);

            if (!reliable_data_packets_sent.TryAdd(id, packet))
            {
                throw new Exception("That shouldn't be possible");
            }
        }

        internal void DisconnectInternalPacket(InfinityInternalErrors _error, string _reason)
        {
            DisconnectInternal(_error, _reason);
        }
    }
}
