namespace Infinity.Core
{
    public struct DataReceivedEventArgs
    {
        public readonly NetworkConnection Connection;
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
        public readonly string Reason;

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
        public readonly MessageReader HandshakeData;

        public NewConnectionEventArgs(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            HandshakeData = _reader;
        }
    }
}
