using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        internal ConcurrentDictionary<int, MessageReader> OrderedMessagesReceived = new ConcurrentDictionary<int, MessageReader>();

        internal bool receivedFirst = false;

        internal const int OrderedHeaderSize = sizeof(byte) + sizeof(ushort) + sizeof(byte);

        internal volatile int sendSequence = 0;
        internal volatile int receiveSequence = 1;

        void OrderedSend(byte[] data)
        {
            var buffer = new byte[data.Length + OrderedHeaderSize - 3];

            buffer[0] = UdpSendOption.ReliableOrdered;

            AttachReliableID(buffer, 1);

            int before = sendSequence;
            Interlocked.Exchange(ref sendSequence, (sendSequence + 1) % 255);

            buffer[3] = (byte)before;

            Buffer.BlockCopy(data, 3, buffer, OrderedHeaderSize, data.Length - 3);

            WriteBytesToConnection(buffer, buffer.Length);
        }

        void OrderedMessageReceived(MessageReader messageReader)
        {
            if (ProcessReliableReceive(messageReader.Buffer, 1, out var id))
            {
                messageReader.Position += 3;

                int before = messageReader.ReadByte();
                int current = (before + 1) % 255;

                OrderedMessagesReceived.TryAdd(current, messageReader);

                while (OrderedMessagesReceived.TryRemove(receiveSequence, out var reader))
                {
                    InvokeDataReceived(reader, UdpSendOption.ReliableOrdered);

                    Interlocked.Exchange(ref receiveSequence, (receiveSequence + 1) % 255);
                }
            }
        }
    }
}
