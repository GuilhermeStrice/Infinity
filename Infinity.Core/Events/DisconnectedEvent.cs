namespace Infinity.Core
{
    public class DisconnectedEvent : IRecyclable
    {
        public NetworkConnection? Connection;
        public MessageReader? Message;
        public string? Reason;

        internal DisconnectedEvent()
        {
        }

        public static DisconnectedEvent Get()
        {
            return Pools.DisconnectedEventPool.GetObject();
        }

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                Message?.Recycle();
            }

            Connection = null;
            Message = null;
            Reason = string.Empty;

            Pools.DisconnectedEventPool.PutObject(this);
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
