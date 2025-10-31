using Infinity.Core;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        private ConcurrentDictionary<byte, MessageReader> ordered_messages_received = new ConcurrentDictionary<byte, MessageReader>();

        private volatile int send_sequence = 0;
        private volatile int receive_sequence = 0;

        private async Task OrderedSend(byte[] _buffer)
        {
            AttachReliableID(_buffer, 1);

            _buffer[3] = send_sequence;

            await WriteBytesToConnection(_buffer, _buffer.Length);

            send_sequence = (byte)Interlocked.Increment(ref send_sequence);
        }

        private async Task OrderedMessageReceived(MessageReader _reader)
        {
            var result = await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
            if (result.Item1)
            {
                byte ordered_id = _reader.Buffer[3];

                ordered_messages_received.TryAdd(ordered_id, _reader);

                while (ordered_messages_received.TryRemove((byte)receive_sequence, out var ordered_reader))
                {
                    ordered_reader.Position = 3;
                    InvokeDataReceived(ordered_reader);

                    receive_sequence = (byte)Interlocked.Increment(ref receive_sequence);
                }
            }
        }
    }
}
