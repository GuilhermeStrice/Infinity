using Infinity.Core;
using System.Collections.Concurrent;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        internal ConcurrentDictionary<ushort, UdpPacket> reliable_data_packets_sent = new ConcurrentDictionary<ushort, UdpPacket>();

        private int last_id_allocated = -1;

        public int ManageReliablePackets()
        {
            int output = 0;
            if (reliable_data_packets_sent.Count > 0)
            {
                foreach (var id_packet in reliable_data_packets_sent)
                {
                    UdpPacket packet = id_packet.Value;

                    try
                    {
                        output += packet.Resend();
                    }
                    catch { }
                }
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

        private void ReliableMessageReceive(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                InvokeDataReceived(_reader);
            }
            else
            {
                _reader.Recycle();
            }
        }

        private void DisposeReliablePackets()
        {
            foreach (var id_packet in reliable_data_packets_sent)
            {
                ushort id = id_packet.Key;
                if (reliable_data_packets_sent.TryRemove(id, out var packet))
                {
                    packet.Recycle();
                }
            }
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
                    UdpPacket.MinResendDelayMs, UdpPacket.MaxInitialResendDelayMs);
            }

            UdpPacket packet = Pools.PacketPool.GetObject();
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
