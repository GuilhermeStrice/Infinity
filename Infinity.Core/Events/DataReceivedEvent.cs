namespace Infinity.Core
{
    public class DataReceivedEvent : IRecyclable
    {
        public NetworkConnection? Connection;
        public MessageReader? Message;

        internal DataReceivedEvent()
        {
        }

        public static DataReceivedEvent Get()
        {
            return Pools.DataReceivedEventPool.GetObject();
        }

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                Message.Recycle();
            }

            Message = null;
            Connection = null;

            Pools.DataReceivedEventPool.PutObject(this);
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
