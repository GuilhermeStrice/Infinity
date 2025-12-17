namespace Infinity.Core
{
    public struct DataReceivedEvent
    {
        public NetworkConnection? Connection;
        public MessageReader? Message;

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                Message.Recycle();
            }

            Message = null;
            Connection = null;
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
