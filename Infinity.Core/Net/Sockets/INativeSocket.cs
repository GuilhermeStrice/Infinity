namespace Infinity.Core.Net.Sockets
{
    public interface INativeSocket
    {
        public void Connect(string _address, ushort _port);
        public int Receive(byte[] _buffer, int _size, int _flags);
        //public int ReceiveFrom(byte[] _buffer, int _size, int _flags);
        public void Disconnect();
        public void Listen();
        public NativeSocket Accept();
        public void Bind(string _address, ushort _port);
        public void Close();
    }
}
