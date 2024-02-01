using System.Net;
using System.Net.Sockets;

namespace Infinity.SNTP
{
    public class NtpServer
    {
        Socket socket;

        private static DateTime BaseDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private IPEndPoint endPoint;

        public NtpServer(ushort port)
        {
            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);

            endPoint = new IPEndPoint(IPAddress.Any, port);

            socket.Bind(endPoint);
        }

        public void Start()
        {
            StartListeningForData();
        }

        private void StartListeningForData()
        {
            EndPoint ep = endPoint;
            byte[] buffer = new byte[1024];
            socket.BeginReceiveFrom(buffer, 0, 1024, SocketFlags.None, ref ep, HandleReceive, buffer);
        }

        private void HandleReceive(IAsyncResult result)
        {
            var buffer = (byte[])result.AsyncState;

            EndPoint remote_end_point = new IPEndPoint(endPoint.Address, endPoint.Port);
            int received = socket.EndReceiveFrom(result, ref remote_end_point);

            var arrival = DateTime.Now.ToUniversalTime();
            var reference = arrival.AddSeconds(-60);

            StartListeningForData();

            NtpPacket request = NtpPacket.FromBytes(buffer, received);

            NtpPacket response = new NtpPacket();
            response.LeapIndicator = NtpLeapIndicator.NoWarning;
            response.VersionNumber = 4;
            response.Mode = NtpMode.Server;
            response.Stratum = 4;
            response.PollInterval = request.PollInterval;
            response.Precision = 0;
            response.ReferenceId = 1280262988; // LOCL
            response.RootDelay = new TimeSpan(0, 0, 1000);
            response.RootDelay = new TimeSpan(0, 0, 1000);
            response.ReferenceTimestamp = reference;
            response.OriginTimestamp = request.TransmitTimestamp;
            response.ReceiveTimestamp = arrival;
            response.TransmitTimestamp = request.TransmitTimestamp;
            var res_bytes = response.ToBytes();

            socket.BeginSendTo(res_bytes, 0, res_bytes.Length, SocketFlags.None, remote_end_point, HandleSendTo, null);
        }

        private void HandleSendTo(IAsyncResult result)
        {
            socket.EndSendTo(result);
        }
    }
}
