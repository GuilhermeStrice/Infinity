namespace Infinity.Core.Tcp
{
    public class TcpConnectionStatistics
    {
        long bytesSent = 0;
        long bytesReceived = 0;

        long streamsSent = 0;
        long streamsReceived = 0;

        public long BytesSent => Interlocked.Read(ref bytesSent);
        public long BytesReceived => Interlocked.Read(ref bytesReceived);

        public long StreamsSent => Interlocked.Read(ref streamsSent);
        public long StreamsReceived => Interlocked.Read(ref streamsReceived);

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
