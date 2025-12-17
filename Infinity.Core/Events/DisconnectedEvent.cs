namespace Infinity.Core
{
    public struct DisconnectedEvent
    {
        public NetworkConnection? Connection;
        public MessageReader? Message;
        public string? Reason;

        public void Recycle(bool _recycle_message)
        {
            if (_recycle_message)
            {
                Message?.Recycle();
            }

            Connection = null;
            Message = null;
            Reason = string.Empty;
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
