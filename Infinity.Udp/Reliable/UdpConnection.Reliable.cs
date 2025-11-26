using Infinity.Core;
using System.Collections.Concurrent;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        public ConcurrentDictionary<ushort, UdpPacket> reliable_data_packets_sent = new ConcurrentDictionary<ushort, UdpPacket>();

        private int last_id_allocated = -1;

        internal async Task<int> ManageReliablePackets()
        {
            int output = 0;
            if (reliable_data_packets_sent.Count > 0)
            {
                foreach (var id_packet in reliable_data_packets_sent)
                {
                    UdpPacket packet = id_packet.Value;

                    try
                    {
                        output += await packet.Resend().ConfigureAwait(false);
                    }
                    catch { }
                }
            }

            return output;
        }

        protected async Task ReliableSend(MessageWriter _writer, Action _ack_callback = null)
        {
            AttachReliableID(_writer, 1, _ack_callback);
            await WriteBytesToConnection(_writer).ConfigureAwait(false);
            Statistics.LogReliableMessageSent(_writer.Length);
            _writer.Recycle();
        }

        private async Task ReliableMessageReceive(MessageReader _reader)
        {
            var result = await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
            if (result.Item1)
            {
                _reader.Position = 3;
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

        protected void AttachReliableID(MessageWriter _writer, int _offset, Action _ack_callback = null)
        {
            ushort id = (ushort)Interlocked.Increment(ref last_id_allocated);

            int old_position = _writer.Position;
            _writer.Position = _offset;

            _writer.Write(id);

            _writer.Position = old_position;

            int resend_delay_ms = configuration.ResendTimeoutMs;
            if (resend_delay_ms <= 0)
            {
                resend_delay_ms = Math.Clamp((int)(AveragePingMs * configuration.ResendPingMultiplier), 
                    UdpPacket.MinResendDelayMs, UdpPacket.MaxInitialResendDelayMs);
            }

            UdpPacket packet = Pools.PacketPool.GetObject();
            packet.Set(
                this,
                _writer,
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
