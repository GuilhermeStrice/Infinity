namespace Infinity.Core
{
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
}
