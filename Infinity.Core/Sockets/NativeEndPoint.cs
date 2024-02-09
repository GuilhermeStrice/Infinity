namespace Infinity.Core.Sockets
{
    public class NativeEndPoint
    {
        internal NativeIPAddress Address;
        internal ushort Port;

        public NativeEndPoint(NativeIPAddress address, ushort port)
        {
            Address = address;
            Port = port;
        }
    }
}
