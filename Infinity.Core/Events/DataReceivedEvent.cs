namespace Infinity.Core
{
    public class DataReceivedEvent : IRecyclable
    {
        public static ObjectPool<DataReceivedEvent> DataReceivedEventPool = new ObjectPool<DataReceivedEvent>(() => new DataReceivedEvent());

        public NetworkConnection? Connection;
        public MessageReader? Message;

        internal DataReceivedEvent()
        {
        }

        public static DataReceivedEvent Get()
        {
            return DataReceivedEventPool.GetObject();
        }

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                Message.Recycle();
            }

            Message = null;
            Connection = null;

            DataReceivedEventPool.PutObject(this);
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
