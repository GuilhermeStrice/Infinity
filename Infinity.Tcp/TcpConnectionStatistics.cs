namespace Infinity.Core.Tcp
{
    public class TcpConnectionStatistics
    {
        private ulong bytesSent = 0;
        private ulong bytesReceived = 0;

        private ulong streamsSent = 0;
        private ulong streamsReceived = 0;

        public ulong BytesSent => Interlocked.Read(ref bytesSent);
        public ulong BytesReceived => Interlocked.Read(ref bytesReceived);

        public ulong StreamsSent => Interlocked.Read(ref streamsSent);
        public ulong StreamsReceived => Interlocked.Read(ref streamsReceived);

        public void LogStreamSent(int length)
        {
            Interlocked.Increment(ref streamsSent);
            Interlocked.Add(ref bytesSent, (ulong)length);
        }
        public void LogStreamReceived(int length)
        {
            Interlocked.Increment(ref streamsReceived);
            Interlocked.Add(ref bytesReceived, (ulong)length);
        }
    }
}
