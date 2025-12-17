namespace Infinity.Core
{
    public struct NewConnectionEvent
    {
        public NetworkConnection? Connection;
        public MessageReader? HandshakeData;

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
        }

        public void Recycle()
        {
            Recycle(true);
        }
    }
}
