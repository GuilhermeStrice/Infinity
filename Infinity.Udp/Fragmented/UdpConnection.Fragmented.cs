using Infinity.Core;
using System.Collections.Concurrent;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        public int MTU
        {
            get
            {
                return 1024;
            }
        }

        private volatile int last_fragment_id_allocated = 0;

        private ConcurrentDictionary<byte, UdpFragmentedMessage> fragmented_messages_received = new ConcurrentDictionary<byte, UdpFragmentedMessage>();

        private const byte fragment_header_size = sizeof(byte) + sizeof(ushort) + sizeof(int) + sizeof(byte) + sizeof(ushort);

        private async Task FragmentedSend(MessageWriter _writer)
        {
            var fragment_size = MTU - fragment_header_size;

            var fragment_id = (byte)Interlocked.Increment(ref last_fragment_id_allocated);
            
            var fragments_count = (int)Math.Ceiling((_writer.Length - 1) / (double)fragment_size);

            for (ushort i = 0; i < fragments_count; i++)
            {
                var data_length = Math.Min(fragment_size, _writer.Length - 1 - fragment_size * i);
                var fragment_writer = new MessageWriter(allocator);

                fragment_writer.Write(UdpSendOptionInternal.Fragment);

                AttachReliableID(fragment_writer, 1);
                fragment_writer.Position = 3;

                fragment_writer.Write(fragments_count);
                fragment_writer.Write(fragment_id);
                fragment_writer.Write(i);

                int source_offset = fragment_size * i + 1;
                fragment_writer.Write(_writer.Buffer, source_offset, data_length);

                await WriteBytesToConnection(fragment_writer).ConfigureAwait(false);
                await Task.Delay(10);
            }

            if (last_fragment_id_allocated >= byte.MaxValue)
            {
                Interlocked.Exchange(ref last_fragment_id_allocated, 0);
            }
        }

        private async Task FragmentMessageReceive(MessageReader _reader)
        {
            var (result, _id) = ProcessReliableReceive(_reader.Buffer, 1);
            if (result)
            {
                _reader.Position = 3;

                var fragments_count = _reader.ReadInt32();
                var fragmented_message_id = _reader.ReadByte();
                var fragment_index = _reader.ReadUInt16();

                var fragmented_message = fragmented_messages_received.GetOrAdd(fragmented_message_id, id =>
                {
                    var msg = UdpFragmentedMessage.Get();
                    msg.FragmentsCount = fragments_count;
                    return msg;
                });

                fragmented_message.Fragments.TryAdd(fragment_index, _reader);

                if (fragmented_message.Fragments.Count == fragments_count)
                {
                    var writer = UdpMessageFactory.BuildFragmentedMessage(this);
                    writer.Position = 3;

                    foreach (var fragment in fragmented_message.Fragments.OrderBy(fragment => fragment.Key))
                    {
                        var fragment_reader = fragment.Value;
                        writer.Write(fragment_reader.Buffer, fragment_reader.Position, fragment_reader.Length - fragment_reader.Position);
                    }

                    fragmented_message.Recycle();

                    var reader = writer.ToReader();

                    reader.Position = 3;

                    await InvokeBeforeReceive(reader).ConfigureAwait(false);
                    await InvokeDataReceived(reader).ConfigureAwait(false);

                    // remove from dictionary
                    fragmented_messages_received.TryRemove(fragmented_message_id, out var _);
                }
            }
        }
    }
}