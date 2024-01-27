using Infinity.Core;

namespace Infinity.Udp
{
    partial class UdpConnection
    {
        public static int FragmentSizeIPv4
        {
            get
            {
                return 576 - fragment_header_size - 68; // Minimum required by https://datatracker.ietf.org/doc/html/rfc791 - 60 is maximum possible ipv4 header size + 8 bytes for udp header
            }
        }

        public static int FragmentSizeIPv6
        {
            get
            {
                return 1280 - fragment_header_size - 40; // Minimum required by https://datatracker.ietf.org/doc/html/rfc2460 - 40 is ipv6 header size + 8 bytes for udp header
            }
        }

        private volatile int last_fragment_id_allocated = 0;

        private FasterConcurrentDictionary<ushort, FragmentedMessage> fragmented_messages_received = new FasterConcurrentDictionary<ushort, FragmentedMessage>();

        private const byte fragment_header_size = sizeof(byte) + sizeof(ushort) + sizeof(byte) + sizeof(byte);

        private void FragmentedSend(byte[] _buffer)
        {
            var fragment_size = BufferSize - fragment_header_size;

            var fragment_id = (byte)Interlocked.Increment(ref last_fragment_id_allocated);
            
            var fragments_count = (int)Math.Ceiling(_buffer.Length / (double)fragment_size);

            if (fragments_count >= byte.MaxValue)
            {
                throw new InfinityException("Too many fragments");
            }

            for (ushort i = 0; i < fragments_count; i++)
            {
                var data_length = Math.Min(fragment_size, _buffer.Length - fragment_size * i);
                var fragment_buffer = new byte[data_length + fragment_header_size];

                fragment_buffer[0] = UdpSendOptionInternal.Fragment;

                AttachReliableID(fragment_buffer, 1);

                fragment_buffer[3] = (byte)fragments_count;
                fragment_buffer[4] = fragment_id;

                Buffer.BlockCopy(_buffer, fragment_size * i, fragment_buffer, fragment_header_size, data_length);
                
                WriteBytesToConnection(fragment_buffer, fragment_buffer.Length);
            }

            if (last_fragment_id_allocated >= byte.MaxValue)
            {
                Interlocked.Exchange(ref last_fragment_id_allocated, 0);
            }
        }

        private void FragmentMessageReceive(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                _reader.Position += 3;

                var fragments_count = _reader.ReadByte();
                var fragmented_id = _reader.ReadByte();

                if (!fragmented_messages_received.TryGetValue(fragmented_id, out var fragmented_message))
                {
                    fragmented_message = FragmentedMessage.Get();
                    fragmented_message.FragmentsCount = fragments_count;

                    fragmented_messages_received.TryAdd(fragmented_id, fragmented_message);
                }

                var fragment = Fragment.Get();
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

                        var reader = writer.ToReader();
                        writer.Recycle();

                        InvokeBeforeReceive(reader);
                        InvokeDataReceived(reader);

                        FragmentedMessage reference;
                        fragmented_messages_received.Remove(fragmented_id, out reference);

                        reference.Recycle();
                    }
                }
            }
        }
    }
}