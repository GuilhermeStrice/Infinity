using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        private MessageReader[] ordered_messages_received = new MessageReader[byte.MaxValue];

        private volatile int send_sequence = 0;
        private volatile int receive_sequence = 1;

        private void OrderedSend(byte[] _buffer)
        {
            AttachReliableID(_buffer, 1);

            _buffer[3] = (byte)send_sequence;

            WriteBytesToConnection(_buffer, _buffer.Length);

            Interlocked.Exchange(ref send_sequence, (send_sequence + 1) % 255);
        }

        private void OrderedMessageReceived(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                int current = (_reader.Buffer[3] + 1) % 255;

                ordered_messages_received[current] = _reader;

                while (ordered_messages_received[receive_sequence] != null)
                {
                    InvokeDataReceived(ordered_messages_received[receive_sequence]);

                    ordered_messages_received[receive_sequence] = null;

                    Interlocked.Exchange(ref receive_sequence, (receive_sequence + 1) % 255);
                }
            }
        }
    }
}
