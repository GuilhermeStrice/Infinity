namespace Infinity.Core
{
    public class ListenerStatistics
    {
        private int _receiveThreadBlocked;
        public int ReceiveThreadBlocked => _receiveThreadBlocked;

        private long _bytesSent;
        public long BytesSent => _bytesSent;

        public void AddReceiveThreadBlocking()
        {
            Interlocked.Increment(ref _receiveThreadBlocked);
        }

        public void AddBytesSent(long bytes)
        {
            Interlocked.Add(ref _bytesSent, bytes);
        }
    }
}
