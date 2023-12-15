using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        /// <summary>
        ///     The amount of data that can be put into a fragment if in IPv4 mode
        /// </summary>
        public static int FragmentSizeIPv4
        {
            get
            {
                return 576 - fragment_header_size; // Minimum required by https://datatracker.ietf.org/doc/html/rfc791
            }
        }

        /// <summary>
        ///     The amount of data that can be put into a fragment if in IPv6 mode
        /// </summary>
        public static int FragmentSizeIPv6
        {
            get
            {
                return 1280 - fragment_header_size; // Minimum required by https://datatracker.ietf.org/doc/html/rfc2460
            }
        }

        /// <summary>
        ///     The last fragmented message ID that was written.
        /// </summary>
        volatile int last_fragment_id_allocated = 0;

        ConcurrentDictionary<ushort, FragmentedMessage> fragmented_messages_received = new ConcurrentDictionary<ushort, FragmentedMessage>();

        private const byte fragment_header_size = sizeof(byte) + sizeof(ushort) + sizeof(ushort) + sizeof(ushort);

        /// <summary>
        ///     Sends a message fragmenting it as needed to pass over the network.
        /// </summary>
        /// <param name="sendOption">The send option the message was sent with.</param>
        /// <param name="_buffer">The data of the message to send.</param>
        void FragmentedSend(byte[] _buffer)
        {
            var id = (ushort)Interlocked.Increment(ref last_fragment_id_allocated);
            var mtu = (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6);

            var fragments_count = (int)Math.Ceiling(_buffer.Length / (double)mtu);

            if (fragments_count >= ushort.MaxValue)
            {
                throw new InfinityException("Too many fragments");
            }

            for (ushort i = 0; i < fragments_count; i++)
            {
                var data_length = Math.Min(mtu, _buffer.Length - mtu * i);
                var buffer = new byte[data_length + fragment_header_size];

                buffer[0] = UdpSendOptionInternal.Fragment;

                AttachReliableID(buffer, 1);

                buffer[3] = (byte)fragments_count;
                buffer[4] = (byte)(fragments_count >> 8);

                buffer[5] = (byte)id;
                buffer[6] = (byte)(id >> 8);

                Buffer.BlockCopy(_buffer, mtu * i, buffer, fragment_header_size, data_length);
                
                WriteBytesToConnection(buffer, buffer.Length);
            }
        }

        void FragmentMessageReceive(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                _reader.Position += 3;

                var fragments_count = _reader.ReadUInt16();
                var fragmented_message_id = _reader.ReadUInt16();

                if (!fragmented_messages_received.TryGetValue(fragmented_message_id, out var fragmented_message))
                {
                    fragmented_message = FragmentedMessage.Get();
                    fragmented_message.FragmentsCount = fragments_count;

                    fragmented_messages_received.TryAdd(fragmented_message_id, fragmented_message);
                }

                var fragment = Fragment.Get();
                fragment.Id = id;
                fragment.Reader = _reader;

                lock (fragmented_message.Fragments)
                {
                    fragmented_message.Fragments.Add(fragment);

                    if (fragmented_message.Fragments.Count == fragments_count)
                    {
                        var writer = UdpMessageFactory.BuildFragmentedMessage();

                        foreach (var f in fragmented_message.Fragments.OrderBy(fragment => fragment.Id))
                        {
                            writer.Write(f.Reader.Buffer, f.Reader.Position, f.Reader.Length - f.Reader.Position);
                        }

                        var reader = writer.AsReader();
                        writer.Recycle();

                        InvokeBeforeReceive(reader);
                        InvokeDataReceived(reader);

                        FragmentedMessage reference;
                        fragmented_messages_received.Remove(fragmented_message_id, out reference);

                        reference.Recycle();
                    }
                }
            }
        }
    }
}