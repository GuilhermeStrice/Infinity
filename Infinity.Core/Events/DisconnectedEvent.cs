namespace Infinity.Core
{
    public class DisconnectedEvent : IRecyclable
    {
        public NetworkConnection Connection;
        public string Reason;

        public MessageReader Message;

        internal DisconnectedEvent()
        {
        }

        public static DisconnectedEvent Get()
        {
            return Pools.DisconnectedEventPool.GetObject();
        }

        public void Recycle()
        {
            Message?.Recycle();

            Pools.DisconnectedEventPool.PutObject(this);
        }
    }
}
