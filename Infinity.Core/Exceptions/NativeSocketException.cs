using Infinity.Core.Sockets;

namespace Infinity.Core.Exceptions
{
    public class NativeSocketException : Exception
    {
        public SocketError ErrorCode { get; private set; }

        internal NativeSocketException() : base()
        {
        }

        internal NativeSocketException(string _message) : base(_message)
        {
        }

        internal NativeSocketException(string _message, SocketError _error_code) : base(_message)
        {
            ErrorCode = _error_code;
        }

        internal NativeSocketException(SocketError _error_code) : this("Socket error", _error_code)
        {
        }
    }
}
