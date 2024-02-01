using System.Net.Sockets;

namespace Infinity.SNTP
{
    public class NtpServer
    {
        Socket socket;

        public NtpServer()
        {
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
        }
    }
}
