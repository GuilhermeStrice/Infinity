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
}
