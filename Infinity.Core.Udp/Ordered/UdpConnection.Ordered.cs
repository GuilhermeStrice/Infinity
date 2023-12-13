using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        private ConcurrentDictionary<int, MessageReader> ordered_messages_received = new ConcurrentDictionary<int, MessageReader>();

        private const int ordered_header_size = sizeof(byte) + sizeof(ushort) + sizeof(byte);

        internal volatile int send_sequence = 0;
        internal volatile int receive_sequence = 1;

        void OrderedSend(byte[] data)
        {
            var buffer = new byte[data.Length + ordered_header_size - 3];

            buffer[0] = UdpSendOption.ReliableOrdered;

            AttachReliableID(buffer, 1);

            int before = send_sequence;
            Interlocked.Exchange(ref send_sequence, (send_sequence + 1) % 255);

            buffer[3] = (byte)before;

            Buffer.BlockCopy(data, 3, buffer, ordered_header_size, data.Length - 3);

            WriteBytesToConnection(buffer, buffer.Length);
        }

        void OrderedMessageReceived(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                _reader.Position += 3;

                int before = _reader.ReadByte();
                int current = (before + 1) % 255;

                ordered_messages_received.TryAdd(current, _reader);

                while (ordered_messages_received.TryRemove(receive_sequence, out var reader))
                {
                    InvokeDataReceived(reader);

                    Interlocked.Exchange(ref receive_sequence, (receive_sequence + 1) % 255);
                }
            }
        }
    }
}
