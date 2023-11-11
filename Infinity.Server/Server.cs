using Infinity.Core;
using Infinity.Core.Tcp;
using System.Net;

namespace Infinity.Server
{
    public class Server
    {
        public delegate void ServerDelegate(object sender, EventArgs e);

        private TcpConnectionListener tcpListener;
        private UdpConnectionListener udpListener;

        public int TcpPort { get; set; }
        public int UdpPort { get; set; }

        public bool UseUdp { get; set; }
        public bool UseTcp { get; set; }

        private IPMode ipMode;
        private IPAddress ipAddress;

        public Server(IPMode ipMode)
        {
            this.ipMode = ipMode;
            ipAddress = (ipMode == IPMode.IPv6 ? IPAddress.Any : IPAddress.IPv6Any);
        }

        public void Start()
        {
            tcpListener = new TcpConnectionListener(new IPEndPoint(ipAddress, TcpPort), ipMode);
            udpListener = new UdpConnectionListener(new IPEndPoint(ipAddress, UdpPort), ipMode);

            tcpListener.NewConnection += TcpListener_NewConnection;
        }

        private void TcpListener_NewConnection(NewConnectionEventArgs obj)
        {
            throw new NotImplementedException();
        }
    }
}
