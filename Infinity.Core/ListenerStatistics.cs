namespace Infinity.Core
{
    public class ListenerStatistics
    {
        private long _bytesSent;
        public long BytesSent => _bytesSent;

        public void AddBytesSent(long bytes)
        {
            Interlocked.Add(ref _bytesSent, bytes);
        }
    }
}
