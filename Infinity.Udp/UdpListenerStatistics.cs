namespace Infinity.Udp
{
    public class UdpListenerStatistics
    {
        private ulong bytes_sent;
        public ulong BytesSent => bytes_sent;

        public void AddBytesSent(int bytes)
        {
            Interlocked.Add(ref bytes_sent, (ulong)bytes);
        }
    }
}
