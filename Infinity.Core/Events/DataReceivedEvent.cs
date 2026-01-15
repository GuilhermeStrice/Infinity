namespace Infinity.Core
{
    public struct DataReceivedEvent
    {
        public NetworkConnection Connection;
        public MessageReader Message;
    }
}
