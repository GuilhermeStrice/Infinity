namespace Infinity.Core.Exceptions
{
    internal class NativeSocketException : Exception
    {
        public int ErrorCode { get; private set; }

        public NativeSocketException() : base()
        {
        }

        public NativeSocketException(string _message) : base(_message)
        {
        }

        public NativeSocketException(string _message, int _error_code) : base(_message)
        {
            ErrorCode = _error_code;
        }
    }
}
