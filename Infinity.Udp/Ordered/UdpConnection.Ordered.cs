using Infinity.Core;
using System.Collections.Concurrent;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        private ConcurrentDictionary<byte, MessageReader> ordered_messages_received = new ConcurrentDictionary<byte, MessageReader>();

        private volatile byte send_sequence = 0;
        private volatile byte receive_sequence = 0;

        private void OrderedSend(byte[] _buffer)
        {
            KeepAliveTimerWait();

            AttachReliableID(_buffer, 1);

            _buffer[3] = send_sequence;

            WriteBytesToConnection(_buffer, _buffer.Length);

            send_sequence++;
        }

        private void OrderedMessageReceived(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                byte ordered_id = _reader.Buffer[3];

                ordered_messages_received.TryAdd(ordered_id, _reader);

                while (ordered_messages_received.TryRemove(receive_sequence, out var ordered_reader))
                {
                    ordered_reader.Position = 3;
                    InvokeDataReceived(ordered_reader);

                    receive_sequence++;
                }
            }
        }
    }
}
