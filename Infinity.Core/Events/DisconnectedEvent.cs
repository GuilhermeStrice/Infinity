namespace Infinity.Core
{
    public struct DisconnectedEvent
    {
        public NetworkConnection Connection;
        public MessageReader Message;
        public string? Reason;
    }
}
