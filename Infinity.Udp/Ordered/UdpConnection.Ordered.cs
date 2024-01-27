using Infinity.Core;

namespace Infinity.Udp
{
    partial class UdpConnection
    {
        private MessageReader[] ordered_messages_received = new MessageReader[byte.MaxValue];

        private volatile int send_sequence = 0;
        private volatile int receive_sequence = 0;

        private void OrderedSend(byte[] _buffer)
        {
            AttachReliableID(_buffer, 1);

            _buffer[3] = (byte)send_sequence;

            WriteBytesToConnection(_buffer, _buffer.Length);

            send_sequence++;

            if (send_sequence >= 256)
            {
                send_sequence = 0;
            }
        }

        private void OrderedMessageReceived(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                int ordered_id = _reader.Buffer[3];

                lock (ordered_messages_received)
                {
                    ordered_messages_received[ordered_id] = _reader;

                    while (ordered_messages_received[receive_sequence] != null)
                    {
                        InvokeDataReceived(ordered_messages_received[receive_sequence]);

                        ordered_messages_received[receive_sequence] = null;

                        receive_sequence++;

                        if (receive_sequence >= 256)
                        {
                            receive_sequence = 0;
                        }
                    }
                }
            }
        }
    }
}
