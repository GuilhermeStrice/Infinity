using Infinity.Core.Udp.Fragmented;
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
                return 576 - FragmentHeaderSize; // Minimum required by https://datatracker.ietf.org/doc/html/rfc791
            }
        }

        /// <summary>
        ///     The amount of data that can be put into a fragment if in IPv6 mode
        /// </summary>
        public static int FragmentSizeIPv6
        {
            get
            {
                return 1280 - FragmentHeaderSize; // Minimum required by https://datatracker.ietf.org/doc/html/rfc2460
            }
        }

        /// <summary>
        ///     The last fragmented message ID that was written.
        /// </summary>
        volatile int lastFragmentIDAllocated;

        ConcurrentDictionary<ushort, FragmentedMessage> fragmentedMessagesReceived = new ConcurrentDictionary<ushort, FragmentedMessage>();

        private const byte FragmentHeaderSize = sizeof(byte) + sizeof(ushort) + sizeof(ushort) + sizeof(ushort);

        /// <summary>
        ///     Sends a message fragmenting it as needed to pass over the network.
        /// </summary>
        /// <param name="sendOption">The send option the message was sent with.</param>
        /// <param name="data">The data of the message to send.</param>
        void FragmentedSend(byte[] data)
        {
            var id = (ushort)Interlocked.Increment(ref lastFragmentIDAllocated);
            var mtu = (IPMode == IPMode.IPv4 ? FragmentSizeIPv4 : FragmentSizeIPv6);
            var fragmentsCount = (int)Math.Ceiling(data.Length / (double)mtu);

            if (fragmentsCount >= ushort.MaxValue)
            {
                throw new InfinityException("Too many fragments");
            }

            for (ushort i = 0; i < fragmentsCount; i++)
            {
                var dataLength = Math.Min(mtu, data.Length - mtu * i);
                var buffer = new byte[dataLength + FragmentHeaderSize];

                buffer[0] = UdpSendOptionInternal.Fragment;

                AttachReliableID(buffer, 1);

                buffer[3] = (byte)fragmentsCount;
                buffer[4] = (byte)(fragmentsCount >> 8);

                buffer[5] = (byte)id;
                buffer[6] = (byte)(id >> 8);

                Buffer.BlockCopy(data, mtu * i, buffer, FragmentHeaderSize, dataLength);
                
                WriteBytesToConnection(buffer, buffer.Length);
            }
        }

        protected void FragmentMessageReceive(MessageReader messageReader)
        {
            if (ProcessReliableReceive(messageReader.Buffer, 1, out var id))
            {
                messageReader.Position += 3;

                var fragmentsCount = messageReader.ReadUInt16();
                var fragmentedMessageId = messageReader.ReadUInt16();

                if (!fragmentedMessagesReceived.TryGetValue(fragmentedMessageId, out var fragmentedMessage))
                {
                    fragmentedMessage = FragmentedMessage.Get();
                    fragmentedMessage.FragmentsCount = fragmentsCount;

                    fragmentedMessagesReceived.TryAdd(fragmentedMessageId, fragmentedMessage);
                }

                var buffer = new byte[messageReader.Length - messageReader.Position];
                Buffer.BlockCopy(messageReader.Buffer, messageReader.Position, buffer, 0, messageReader.Length - messageReader.Position);

                var fragment = Fragment.Get();
                fragment.Id = id;
                fragment.Data = buffer;

                // locking a HashSet is faster than anything else
                int fragmentedMessageFragmentsCount = 0;
                lock (fragmentedMessage.Fragments)
                {
                    fragmentedMessage.Fragments.Add(fragment);
                    fragmentedMessageFragmentsCount = fragmentedMessage.Fragments.Count;
                }

                if (fragmentedMessageFragmentsCount == fragmentsCount)
                {
                    var reconstructed = fragmentedMessage.Reconstruct();
                    InvokeDataReceived(MessageReader.Get(reconstructed), UdpSendOption.Reliable);

                    FragmentedMessage reference;
                    fragmentedMessagesReceived.Remove(fragmentedMessageId, out reference);

                    reference.Recycle();
                }
            }
        }
    }
}