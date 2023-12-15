namespace Infinity.Core.Udp
{
    public static class UdpMessageFactory
    {
        public static MessageWriter BuildReliableMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOption.Reliable);
            writer.Position += 2; // Reliable id goes here

            return writer;
        }

        public static MessageWriter BuildOrderedMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOption.ReliableOrdered);

            writer.Position += 3; // Reliable id and the before id

            return writer;
        }

        public static MessageWriter BuildFragmentedMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOption.Fragmented);
            writer.Position += 2; // reliable id
            
            return writer;
        }
    }
}
