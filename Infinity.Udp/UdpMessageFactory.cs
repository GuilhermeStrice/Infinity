using Infinity.Core;

namespace Infinity.Udp
{
    public static class UdpMessageFactory
    {
        public static MessageWriter BuildHandshakeMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);

            writer.Write(UdpSendOptionInternal.Handshake);
            writer.Position = 3;

            return writer;
        }

        public static MessageWriter BuildUnreliableMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);

            writer.Write(UdpSendOption.Unreliable);

            return writer;
        }

        public static MessageWriter BuildReliableMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);

            writer.Write(UdpSendOption.Reliable);
            writer.Position = 3; // Reliable id goes here

            return writer;
        }

        public static MessageWriter BuildOrderedMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);

            writer.Write(UdpSendOption.ReliableOrdered);

            writer.Position = 4; // Reliable id and the before id

            return writer;
        }

        public static MessageWriter BuildFragmentedMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);
            writer.Write(UdpSendOption.Fragmented);
            
            return writer;
        }

        public static MessageWriter BuildDisconnectMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);

            writer.Write(UdpSendOption.Disconnect);

            return writer;
        }

        internal static MessageWriter BuildAskConfirurationMessage(UdpConnection connection)
        {
            MessageWriter writer = new MessageWriter(connection.allocator);

            writer.Write(UdpSendOptionInternal.AskConfiguration);

            return writer;
        }

        internal static MessageReader BuildEmptyReader(UdpConnection connection)
        {
            MessageReader reader = new MessageReader(connection.allocator);
            return reader;
        }
    }
}
