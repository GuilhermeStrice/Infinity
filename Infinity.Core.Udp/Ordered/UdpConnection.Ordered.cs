using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        internal ConcurrentDictionary<int, MessageReader> OrderedMessagesReceived = new ConcurrentDictionary<int, MessageReader>();

        internal volatile int nextInSequence = 1;
        internal bool receivedFirst = false;

        internal const int OrderedHeaderSize = sizeof(byte) + sizeof(ushort) + sizeof(byte) + sizeof(byte);

        void OrderedSend(byte[] data)
        {
            var buffer = new byte[data.Length + OrderedHeaderSize - 3];

            buffer[0] = UdpSendOption.ReliableOrdered;

            AttachReliableID(buffer, 1);

            int before;
            int after;

            if (nextInSequence == byte.MaxValue)
            {
                before = nextInSequence;
                after = Interlocked.Exchange(ref nextInSequence, 1);
            }
            else
            {
                before = nextInSequence - 1;
                after = Interlocked.Increment(ref nextInSequence);
            }

            buffer[3] = (byte)before;
            buffer[4] = (byte)after;

            Buffer.BlockCopy(data, 3, buffer, OrderedHeaderSize, data.Length - 3);

            WriteBytesToConnection(buffer, buffer.Length);
        }

        void OrderedMessageReceived(MessageReader messageReader)
        {
            if (ProcessReliableReceive(messageReader.Buffer, 1, out var id))
            {
                messageReader.Position += 3;

                int beforeId = messageReader.ReadByte();
                int afterId = messageReader.ReadByte();
                int currentId = afterId - 1;

                OrderedMessagesReceived.TryAdd(currentId, messageReader);

                if (!OrderedMessagesReceived.ContainsKey(beforeId))
                {
                    if (!receivedFirst)
                    {
                        receivedFirst = true;
                        // just process it
                        InvokeOrderedMessageReceived(currentId);
                        return;
                    }
                    else
                    {
                        // we still havent received the before packet
                        // do nothing
                    }
                }
                else
                {
                    InvokeOrderedMessageReceived(beforeId);
                }
            }
        }

        void InvokeOrderedMessageReceived(int current)
        {
            OrderedMessagesReceived.Remove(current, out var message);

            InvokeDataReceived(message, UdpSendOption.ReliableOrdered);
        }
    }
}
