namespace Infinity.Core
{
    public class DataReceivedEvent
    {
        public NetworkConnection Connection;
        public MessageReader Message;

        public DataReceivedEvent(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            Message = _reader;
        }
    }

    public class DisconnectedEvent
    {
        public NetworkConnection Connection;
        public string Reason;

        public MessageReader Message;

        public DisconnectedEvent(NetworkConnection _connection, string _reason, MessageReader _reader)
        {
            Connection = _connection;
            Reason = _reason;
            Message = _reader;
        }
    }

    public class NewConnectionEvent
    {
        public NetworkConnection Connection;
        public MessageReader HandshakeData;

        public NewConnectionEvent(NetworkConnection _connection, MessageReader _reader)
        {
            Connection = _connection;
            HandshakeData = _reader;
        }
    }
}
