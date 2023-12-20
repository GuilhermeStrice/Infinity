namespace Infinity.Core.Udp
{
    public class UdpListenerStatistics
    {
        private long bytes_sent;
        public long BytesSent => bytes_sent;

        public void AddBytesSent(long bytes)
        {
            Interlocked.Add(ref bytes_sent, bytes);
        }
    }
}
