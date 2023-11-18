namespace Infinity.Core.Tcp
{
    public class TcpConnectionStatistics
    {
        public long BytesSent => Interlocked.Read(ref bytesSent);
        long bytesSent = 0;
        public long BytesReceived => Interlocked.Read(ref bytesReceived);
        long bytesReceived = 0;

        public int StreamsSent => streamsSent;
        private int streamsSent = 0;
        public int StreamsReceived => streamsReceived;
        private int streamsReceived = 0;
        public void LogStreamSent(int length)
        {
            Interlocked.Increment(ref streamsSent);
            Interlocked.Add(ref bytesSent, length);
        }
        public void LogStreamReceived(int length)
        {
            Interlocked.Increment(ref streamsReceived);
            Interlocked.Add(ref bytesReceived, length);
        }
    }
}
