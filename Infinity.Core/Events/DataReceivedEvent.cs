namespace Infinity.Core
{
    public class DataReceivedEvent : IRecyclable
    {
        public NetworkConnection Connection;
        public MessageReader Message;

        internal DataReceivedEvent()
        {
        }

        public static DataReceivedEvent Get()
        {
            return Pools.DataReceivedEventPool.GetObject();
        }

        public void Recycle()
        {
            Message.Recycle();

            Pools.DataReceivedEventPool.PutObject(this);
        }
    }
}
