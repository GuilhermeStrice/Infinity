namespace Infinity.Core
{
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
