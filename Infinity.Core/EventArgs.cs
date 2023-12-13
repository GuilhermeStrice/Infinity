namespace Infinity.Core
{
    public struct DataReceivedEventArgs
    {
        public readonly NetworkConnection Connection;

        /// <summary>
        ///     The bytes received from the client.
        /// </summary>
        public readonly MessageReader Message;

        /// <summary>
        ///     The <see cref="SendOption"/> the data was sent with.
        /// </summary>
        public readonly byte SendOption;

        public DataReceivedEventArgs(NetworkConnection connection, MessageReader msg, byte sendOption)
        {
            Connection = connection;
            Message = msg;
            SendOption = sendOption;
        }
    }

    public struct DisconnectedEventArgs
    {
        public readonly NetworkConnection Connection;

        /// <summary>
        /// Optional disconnect reason. May be null.
        /// </summary>
        public readonly string Reason;

        /// <summary>
        /// Optional data sent with a disconnect message. May be null. 
        /// You must not recycle this. If you need the message outside of a callback, you should copy it.
        /// </summary>
        public readonly MessageReader Message;

        public DisconnectedEventArgs(NetworkConnection connection, string reason, MessageReader message)
        {
            Connection = connection;
            Reason = reason;
            Message = message;
        }
    }

    public struct NewConnectionEventArgs
    {
        public readonly NetworkConnection Connection;

        /// <summary>
        /// The data received from the client in the handshake.
        /// You must not recycle this. If you need the message outside of a callback, you should copy it.
        /// </summary>
        public readonly MessageReader HandshakeData;

        public NewConnectionEventArgs(NetworkConnection connection, MessageReader handshakeData)
        {
            Connection = connection;
            HandshakeData = handshakeData;
        }
    }
}
