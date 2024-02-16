using Infinity.Core;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        private volatile int last_fragment_id_allocated = 0;

        private UdpFragmentedMessage[] fragmented_messages_received = new UdpFragmentedMessage[byte.MaxValue];

        private const byte fragment_header_size = sizeof(byte) + sizeof(ushort) + sizeof(int) + sizeof(byte);

        private void FragmentedSend(byte[] _buffer)
        {
            KeepAliveTimerWait();

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

                Buffer.BlockCopy(_buffer, fragment_size * i, fragment_buffer, fragment_header_size, data_length);
                
                WriteBytesToConnection(fragment_buffer, fragment_buffer.Length);
            }

            if (last_fragment_id_allocated >= byte.MaxValue)
            {
                last_fragment_id_allocated = 0;
            }
        }

        private void FragmentMessageReceive(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                _reader.Position += 3;

                var fragments_count = _reader.ReadInt32();
                var fragmented_message_id = _reader.ReadByte();

                UdpFragmentedMessage fragmented_message;

                if (fragmented_messages_received[fragmented_message_id] != null)
                {
                    fragmented_message = fragmented_messages_received[fragmented_message_id];
                }
                else
                {
                    fragmented_message = UdpFragmentedMessage.Get();
                    fragmented_message.FragmentsCount = fragments_count;

                    fragmented_messages_received[fragmented_message_id] = fragmented_message;
                }

                var fragment = UdpFragment.Get();
                fragment.Id = id;
                fragment.Reader = _reader;

                lock (fragmented_message)
                {
                    fragmented_message.Fragments.Add(fragment);

                    if (fragmented_message.Fragments.Count == fragments_count)
                    {
                        var writer = UdpMessageFactory.BuildFragmentedMessage();

                        foreach (var f in fragmented_message.Fragments.OrderBy(fragment => fragment.Id))
                        {
                            writer.Write(f.Reader.Buffer, f.Reader.Position, f.Reader.Length - f.Reader.Position);
                        }

                        fragmented_message.Recycle();

                        var reader = writer.ToReader();
                        writer.Recycle();

                        InvokeBeforeReceive(reader);
                        InvokeDataReceived(reader);

                        fragmented_messages_received[fragmented_message_id] = null;
                    }
                }
            }
        }
    }
}