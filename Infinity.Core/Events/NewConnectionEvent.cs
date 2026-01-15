namespace Infinity.Core
{
    public struct NewConnectionEvent
    {
        public NetworkConnection Connection;
        public MessageReader HandshakeData;
    }
}
