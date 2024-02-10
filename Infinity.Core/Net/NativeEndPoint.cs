using Infinity.Core.Net.Sockets;
using System.Globalization;

namespace Infinity.Core.Net
{
    public class NativeEndPoint
    {
        public NativeIPAddress Address { get; set; }
        public ushort Port { get; set; }

        public AddressFamily AddressFamily
        {
            get
            {
                return Address.AddressFamily;
            }
        }

        public NativeEndPoint(long address, ushort port)
        {
            Port = port;
            Address = new NativeIPAddress(address);
        }

        public NativeEndPoint(NativeIPAddress address, ushort port)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            Port = port;
            Address = address;
        }

        public override string ToString()
        {
            string format;
            if (Address.AddressFamily == AddressFamily.InterNetworkV6)
                format = "[{0}]:{1}";
            else
                format = "{0}:{1}";
            return string.Format(format, Address.ToString(), Port.ToString(NumberFormatInfo.InvariantInfo));
        }

        public NativeSocketAddress Serialize()
        {
            // Let SocketAddress do the bulk of the work
            return new NativeSocketAddress(Address, Port);
        }

        public NativeEndPoint Create(NativeSocketAddress socketAddress)
        {
            // validate SocketAddress
            if (socketAddress.Family != AddressFamily)
            {
                throw new ArgumentException("socketAddress");
            }
            if (socketAddress.Size < 8)
            {
                throw new ArgumentException("socketAddress");
            }

            return socketAddress.GetIPEndPoint();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is NativeEndPoint))
            {
                return false;
            }
            return ((NativeEndPoint)obj).Address.Equals(Address) && ((NativeEndPoint)obj).Port == Port;
        }

        public override int GetHashCode()
        {
            return Address.GetHashCode() ^ Port;
        }
    }
}
