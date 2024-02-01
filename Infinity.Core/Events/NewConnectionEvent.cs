namespace Infinity.Core
{
    public class NewConnectionEvent : IRecyclable
    {
        public NetworkConnection Connection;
        public MessageReader HandshakeData;

        internal NewConnectionEvent()
        {
        }

        public static NewConnectionEvent Get()
        {
            return Pools.NewConnectionPool.GetObject();
        }

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                HandshakeData.Recycle();
            }

            Connection = null;
            HandshakeData = null;

            Pools.NewConnectionPool.PutObject(this);
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
