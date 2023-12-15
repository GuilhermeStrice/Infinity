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

        internal static MessageWriter BuildFragmentMessage(ushort _fragment_count, ushort _fragmented_message_id)
        {
            MessageWriter writer = MessageWriter.Get();

            writer.Write(UdpSendOptionInternal.Fragment);

            writer.Position += 2; // Reliable id

            writer.Write(_fragment_count);
            writer.Write(_fragmented_message_id);

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
