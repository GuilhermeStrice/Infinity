namespace Infinity.Core
{
    public struct DataReceivedEventArgs
    {
        public readonly NetworkConnection Connection;

        /// <summary>
        ///     The bytes received from the client.
        /// </summary>
        public readonly MessageReader Message;

        public DataReceivedEventArgs(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            Message = _reader;
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

        public DisconnectedEventArgs(NetworkConnection _connection, string _reason, MessageReader _reader)
        {
            Connection = _connection;
            Reason = _reason;
            Message = _reader;
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

        public NewConnectionEventArgs(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            HandshakeData = _reader;
        }
    }
}
