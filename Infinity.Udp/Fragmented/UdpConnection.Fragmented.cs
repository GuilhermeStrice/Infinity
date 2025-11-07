using Infinity.Core;
using System.Collections.Concurrent;
using System.Threading;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        private volatile int last_fragment_id_allocated = 0;

        private ConcurrentDictionary<byte, UdpFragmentedMessage> fragmented_messages_received = new ConcurrentDictionary<byte, UdpFragmentedMessage>();

        private const byte fragment_header_size = sizeof(byte) + sizeof(ushort) + sizeof(int) + sizeof(byte) + sizeof(ushort);

        private async Task FragmentedSend(MessageWriter _writer)
        {
            var fragment_size = MTU - fragment_header_size;

            var fragment_id = (byte)Interlocked.Increment(ref last_fragment_id_allocated);
            
            var fragments_count = (int)((_writer.Buffer.Length / (double)fragment_size) + 1);

            for (ushort i = 0; i < fragments_count; i++)
            {
                var data_length = Math.Min(fragment_size, _writer.Buffer.Length - fragment_size * i);
                var fragment_writer = MessageWriter.Get();
                //var fragment_buffer = new byte[data_length + fragment_header_size];

                fragment_writer.Write(UdpSendOptionInternal.Fragment);

                AttachReliableID(fragment_writer, 1);

                fragment_writer.Write(fragments_count);
                fragment_writer.Write(fragment_id);
                fragment_writer.Write(i);

                await WriteBytesToConnection(fragment_writer).ConfigureAwait(false);
            }

            if (last_fragment_id_allocated >= byte.MaxValue)
            {
                Interlocked.Exchange(ref last_fragment_id_allocated, 0);
            }
        }

        private async Task FragmentMessageReceive(MessageReader _reader)
        {
            var result = await ProcessReliableReceive(_reader.Buffer, 1).ConfigureAwait(false);
            if (result.Item1)
            {
                _reader.Position += 3;

                var fragments_count = _reader.ReadInt32();
                var fragmented_message_id = _reader.ReadByte();
                var fragment_index = _reader.ReadUInt16();

                UdpFragmentedMessage fragmented_message;

                if (!fragmented_messages_received.ContainsKey(fragmented_message_id))
                {
                    fragmented_message = UdpFragmentedMessage.Get();
                    fragmented_message.FragmentsCount = fragments_count;

                    fragmented_messages_received.TryAdd(fragmented_message_id, fragmented_message);
                }
                else
                {
                    // reference
                    fragmented_message = fragmented_messages_received[fragmented_message_id];
                }

                fragmented_message.Fragments.TryAdd(fragment_index, _reader);

                if (fragmented_message.Fragments.Count == fragments_count)
                {
                    var writer = UdpMessageFactory.BuildFragmentedMessage();

                    foreach (var fragment in fragmented_message.Fragments.OrderBy(fragment => fragment.Key))
                    {
                        var fragment_reader = fragment.Value;
                        writer.Write(fragment_reader.Buffer, fragment_reader.Position, fragment_reader.Length - fragment_reader.Position);
                    }

                    fragmented_message.Recycle();

                    var reader = writer.ToReader();
                    writer.Recycle();

                    reader.Position = 3;

                    InvokeBeforeReceive(reader);
                    InvokeDataReceived(reader);

                    // remove from dictionary
                    fragmented_messages_received.TryRemove(fragmented_message_id, out var _);
                }
            }
        }
    }
}