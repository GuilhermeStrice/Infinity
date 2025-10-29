using Infinity.Core;
using System.Collections.Concurrent;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        private volatile int last_fragment_id_allocated = 0;

        private ConcurrentDictionary<byte, UdpFragmentedMessage> fragmented_messages_received = new ConcurrentDictionary<byte, UdpFragmentedMessage>();

        private const byte fragment_header_size = sizeof(byte) + sizeof(ushort) + sizeof(int) + sizeof(byte) + sizeof(ushort);

        private async Task FragmentedSend(byte[] _buffer)
        {
            var fragment_size = MTU - fragment_header_size;

            var fragment_id = (byte)++last_fragment_id_allocated;
            
            var fragments_count = (int)((_buffer.Length / (double)fragment_size) + 1);

            for (ushort i = 0; i < fragments_count; i++)
            {
                var data_length = Math.Min(fragment_size, _buffer.Length - fragment_size * i);
                var fragment_buffer = new byte[data_length + fragment_header_size];

                fragment_buffer[0] = UdpSendOptionInternal.Fragment;

                AttachReliableID(fragment_buffer, 1);

                fragment_buffer[3] = (byte)fragments_count;
                fragment_buffer[4] = (byte)(fragments_count >> 8);
                fragment_buffer[5] = (byte)(fragments_count >> 16);
                fragment_buffer[6] = (byte)(fragments_count >> 24);

                fragment_buffer[7] = fragment_id;

                // Add fragment sequence index
                fragment_buffer[8] = (byte)i;
                fragment_buffer[9] = (byte)(i >> 8);

                Array.Copy(_buffer, fragment_size * i, fragment_buffer, fragment_header_size, data_length);
                
                await WriteBytesToConnection(fragment_buffer, fragment_buffer.Length);
            }

            if (last_fragment_id_allocated >= byte.MaxValue)
            {
                last_fragment_id_allocated = 0;
            }
        }

        private async Task FragmentMessageReceive(MessageReader _reader)
        {
            var result = await ProcessReliableReceive(_reader.Buffer, 1);
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