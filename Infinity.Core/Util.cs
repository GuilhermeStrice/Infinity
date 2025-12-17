using System.Net;
using System.Net.Sockets;

namespace Infinity.Core
{
    public static class Util
    {
        public static void FireAndForget(Task task, ILogger logger)
        {
            _ = task.ContinueWith(t =>
            {
                if (t.Exception != null)
                    logger?.WriteError(t.Exception.ToString());
            }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        public static int GetFreePort()
        {
            var udp = new UdpClient(0);
            int port = ((IPEndPoint)udp.Client.LocalEndPoint).Port;
            udp.Close();
            return port;
        }
        
        public static ReadOnlySpan<char> Trim(ReadOnlySpan<char> span)
        {
            int start = 0;
            int end = span.Length - 1;

            while (start <= end && char.IsWhiteSpace(span[start]))
                start++;

            while (end >= start && char.IsWhiteSpace(span[end]))
                end--;

            return span.Slice(start, end - start + 1);
        }
    }
}