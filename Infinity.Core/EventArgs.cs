namespace Infinity.Core
{
    public class DataReceivedEventArgs
    {
        public NetworkConnection Connection;
        public MessageReader Message;

        public DataReceivedEventArgs(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            Message = _reader;
        }
    }

    public class DisconnectedEventArgs
    {
        public NetworkConnection Connection;
        public string Reason;

        public MessageReader Message;

        public DisconnectedEventArgs(NetworkConnection _connection, string _reason, MessageReader _reader)
        {
            Connection = _connection;
            Reason = _reason;
            Message = _reader;
        }
    }

    public class NewConnectionEventArgs
    {
        public NetworkConnection Connection;
        public MessageReader HandshakeData;

        public NewConnectionEventArgs(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            HandshakeData = _reader;
        }
    }
}
