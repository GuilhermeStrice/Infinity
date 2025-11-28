namespace Infinity.Core
{
    public class DisconnectedEvent : IRecyclable
    {
        public static ObjectPool<DisconnectedEvent> DisconnectedEventPool = new ObjectPool<DisconnectedEvent>(() => new DisconnectedEvent());

        public NetworkConnection? Connection;
        public MessageReader? Message;
        public string? Reason;

        internal DisconnectedEvent()
        {
        }

        public static DisconnectedEvent Get()
        {
            return DisconnectedEventPool.GetObject();
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

            DisconnectedEventPool.PutObject(this);
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
