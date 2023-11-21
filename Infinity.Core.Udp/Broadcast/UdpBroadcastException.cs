namespace Infinity.Core.Udp.Broadcast
{
    public class UdpBroadcastException : Exception
    {
        public UdpBroadcastException(string msg) : base(msg)
        {

        }

        public UdpBroadcastException(string msg, Exception e) : base(msg, e)
        {

        }
    }
}
