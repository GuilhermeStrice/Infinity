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
    }
}