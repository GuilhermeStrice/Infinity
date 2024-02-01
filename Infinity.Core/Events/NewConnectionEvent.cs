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

        public void Recycle()
        {
            HandshakeData.Recycle();

            Pools.NewConnectionPool.PutObject(this);
        }
    }
}
