using System.Globalization;

namespace Infinity.Core.Sockets
{
    public class NativeEndPoint
    {
        /// <devdoc>
        ///    <para>
        ///       Specifies the minimum acceptable value for the <see cref='System.Net.IPEndPoint.Port'/>
        ///       property.
        ///    </para>
        /// </devdoc>
        public const int MinPort = 0x00000000;
        /// <devdoc>
        ///    <para>
        ///       Specifies the maximum acceptable value for the <see cref='System.Net.IPEndPoint.Port'/>
        ///       property.
        ///    </para>
        /// </devdoc>
        public const int MaxPort = 0x0000FFFF;

        private NativeIPAddress m_Address;
        private int m_Port;

        internal const int AnyPort = MinPort;

        internal static NativeEndPoint Any = new NativeEndPoint(NativeIPAddress.Any, AnyPort);
        internal static NativeEndPoint IPv6Any = new NativeEndPoint(NativeIPAddress.IPv6Any, AnyPort);


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public AddressFamily AddressFamily
        {
            get
            {
                //
                // IPv6 Changes: Always delegate this to the address we are
                //               wrapping.
                //
                return m_Address.AddressFamily;
            }
        }

        /// <devdoc>
        ///    <para>Creates a new instance of the IPEndPoint class with the specified address and
        ///       port.</para>
        /// </devdoc>
        public NativeEndPoint(long address, int port)
        {
            if (!ValidationHelper.ValidateTcpPort(port))
            {
                throw new ArgumentOutOfRangeException("port");
            }
            m_Port = port;
            m_Address = new NativeIPAddress(address);
        }

        /// <devdoc>
        ///    <para>Creates a new instance of the IPEndPoint class with the specified address and port.</para>
        /// </devdoc>
        public NativeEndPoint(NativeIPAddress address, int port)
        {
            if (address == null)
            {
                throw new ArgumentNullException("address");
            }
            if (!ValidationHelper.ValidateTcpPort(port))
            {
                throw new ArgumentOutOfRangeException("port");
            }
            m_Port = port;
            m_Address = address;
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the IP address.
        ///    </para>
        /// </devdoc>
        public NativeIPAddress Address
        {
            get
            {
                return m_Address;
            }
            set
            {
                m_Address = value;
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Gets or sets the port.
        ///    </para>
        /// </devdoc>
        public int Port
        {
            get
            {
                return m_Port;
            }
            set
            {
                if (!ValidationHelper.ValidateTcpPort(value))
                {
                    throw new ArgumentOutOfRangeException("value");
                }
                m_Port = value;
            }
        }


        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override string ToString()
        {
            string format;
            if (m_Address.AddressFamily == AddressFamily.InterNetworkV6)
                format = "[{0}]:{1}";
            else
                format = "{0}:{1}";
            return String.Format(format, m_Address.ToString(), Port.ToString(NumberFormatInfo.InvariantInfo));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public SocketAddress Serialize()
        {
            // Let SocketAddress do the bulk of the work
            return new SocketAddress(Address, Port);
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public NativeEndPoint Create(SocketAddress socketAddress)
        {
            //
            // validate SocketAddress
            //
            if (socketAddress.Family != this.AddressFamily)
            {
                throw new ArgumentException("socketAddress");
            }
            if (socketAddress.Size < 8)
            {
                throw new ArgumentException("socketAddress");
            }

            return socketAddress.GetIPEndPoint();
        }


        //UEUE
        public override bool Equals(object comparand)
        {
            if (!(comparand is NativeEndPoint))
            {
                return false;
            }
            return ((NativeEndPoint)comparand).m_Address.Equals(m_Address) && ((NativeEndPoint)comparand).m_Port == m_Port;
        }

        //UEUE
        public override int GetHashCode()
        {
            return m_Address.GetHashCode() ^ m_Port;
        }

        // For security, we need to be able to take an IPEndPoint and make a copy that's immutable and not derived.
        internal NativeEndPoint Snapshot()
        {
            return new NativeEndPoint(Address.Snapshot(), Port);
        }
    }
}
