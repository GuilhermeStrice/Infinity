using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        private ConcurrentDictionary<int, MessageReader> ordered_messages_received = new ConcurrentDictionary<int, MessageReader>();

        private const int ordered_header_size = sizeof(byte) + sizeof(ushort) + sizeof(byte);

        internal volatile int send_sequence = 0;
        internal volatile int receive_sequence = 1;

        void OrderedSend(byte[] _buffer)
        {
            AttachReliableID(_buffer, 1);

            _buffer[3] = (byte)send_sequence;

            Interlocked.Exchange(ref send_sequence, (send_sequence + 1) % 255);

            WriteBytesToConnection(_buffer, _buffer.Length);
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
