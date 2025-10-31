using Infinity.Core;
using System.Threading.Tasks;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        private void AcknowledgementMessageReceive(byte[] _bytes, int _bytes_received)
        {
            pings_since_ack = 0;

            ushort id = (ushort)((_bytes[1] << 8) + _bytes[2]);
            AcknowledgeMessageId(id);

            if (_bytes_received == 4)
            {
                byte recent_packets = _bytes[3];
                for (int i = 1; i <= 8; ++i)
                {
                    if ((recent_packets & 1) != 0)
                    {
                        AcknowledgeMessageId((ushort)(id - i));
                    }

                    recent_packets >>= 1;
                }
            }
        }

        private void AcknowledgeMessageId(ushort _id)
        {
            // Dispose of timer and remove from dictionary
            if (reliable_data_packets_sent.TryRemove(_id, out UdpPacket packet))
            {
                Statistics.LogReliablePacketAcknowledged();
                float rt = packet.Stopwatch.ElapsedMilliseconds;

                packet.AckCallback?.Invoke();
                packet.Recycle();

                AveragePingMs = AveragePingMs * .7f + rt * .3f;
            }
            else if (active_pings.TryFindPing(_id, out DateTime pingPkt))
            {
                Statistics.LogReliablePacketAcknowledged();
                float rt = (float)(DateTime.UtcNow - pingPkt).TotalMilliseconds;

                AveragePingMs = AveragePingMs * .7f + rt * .3f;
            }
        }

        private async Task SendAck(ushort _id)
        {
            byte recent_packets = 0;
            for (int i = 1; i <= 8; ++i)
            {
                var index = (ushort)(_id - i);
                if (!reliable_data_packets_missing.ContainsKey(index))
                {
                    recent_packets |= (byte)(1 << (i - 1));
                }
            }

            byte[] bytes = new byte[]
            {
                UdpSendOptionInternal.Acknowledgement,
                (byte)(_id >> 8),
                (byte)(_id >> 0),
                recent_packets
            };

            await WriteBytesToConnection(bytes, bytes.Length);
        }
    }
}
