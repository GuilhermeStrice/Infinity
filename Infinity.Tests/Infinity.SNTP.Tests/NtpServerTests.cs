using System.Net;

namespace Infinity.SNTP.Tests
{
    public class NtpServerTests
    {
        ManualResetEvent mutex = new ManualResetEvent(false);

        [Fact]
        public void TestServer()
        {
            NtpServer server = new NtpServer(1233);

            server.Start();

            NtpClient client = new NtpClient(new IPEndPoint(IPAddress.Loopback, 1233));

            client.OnNtpReceived += Client_OnNtpReceived;
            client.Query();

            mutex.WaitOne(5000);
        }

        private void Client_OnNtpReceived(NtpClock obj)
        {
            mutex.Set();
        }
    }
}
