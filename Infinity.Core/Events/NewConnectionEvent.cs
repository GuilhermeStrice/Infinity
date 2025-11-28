namespace Infinity.Core
{
    public class NewConnectionEvent : IRecyclable
    {
        public static ObjectPool<NewConnectionEvent> NewConnectionPool = new ObjectPool<NewConnectionEvent>(() => new NewConnectionEvent());

        public NetworkConnection? Connection;
        public MessageReader? HandshakeData;

        internal NewConnectionEvent()
        {
        }

        public static NewConnectionEvent Get()
        {
            return NewConnectionPool.GetObject();
        }

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                if (HandshakeData != null)
                {
                    HandshakeData.Recycle();
                }
            }

            Connection = null;
            HandshakeData = null;

            NewConnectionPool.PutObject(this);
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
