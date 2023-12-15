namespace Infinity.Core.Udp
{
    public static class UdpMessageFactory
    {
        public static MessageWriter BuildHandshakeMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOptionInternal.Handshake);
            writer.Position = writer.Length += 2;

            return writer;
        }

        public static MessageWriter BuildReliableMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOption.Reliable);
            writer.Position = writer.Length += 2; // Reliable id goes here

            return writer;
        }

        public static MessageWriter BuildOrderedMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOption.ReliableOrdered);

            writer.Position = writer.Length += 3; // Reliable id and the before id

            return writer;
        }

        public static MessageWriter BuildFragmentedMessage()
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOption.Fragmented);
            writer.Position = writer.Length += 2; // reliable id
            
            return writer;
        }
    }
}
