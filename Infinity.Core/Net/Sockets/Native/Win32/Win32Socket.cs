using Infinity.Core.Exceptions;

namespace Infinity.Core.Net.Sockets.Native.Win32
{
    internal class Win32Socket : INativeSocket
    {
        private readonly AddressFamily address_family;
        private readonly SocketType socket_type;
        private readonly ProtocolType protocol_type;
        private readonly nint socket;

        public Win32Socket(AddressFamily _address_family, SocketType _socket_type, ProtocolType _protocol_type)
        {
            address_family = _address_family;
            socket_type = _socket_type;
            protocol_type = _protocol_type;

            var status = Winsock2.WSAStartup(0x0202, out var wsaData);
            if (status != 0)
            {
                throw new NativeSocketException("WSAStartup failed", status);
            }

            socket = Winsock2.socket(address_family, socket_type, protocol_type);

            if (socket == -1)
            {
                throw new NativeSocketException("Socket is invalid", Winsock2.WSAGetLastError());
            }
        }

        public int Receive(byte[] _buffer, int _size, int _flags)
        {
            throw new NotImplementedException();
        }

        public void Disconnect()
        {
            throw new NotImplementedException();
        }

        public void Listen()
        {
            throw new NotImplementedException();
        }

        NativeSocket INativeSocket.Accept()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Connect(string _address, ushort _port)
        {
            throw new NotImplementedException();
        }

        public void Bind(string _address, ushort _port)
        {
            throw new NotImplementedException();
        }
    }
}
