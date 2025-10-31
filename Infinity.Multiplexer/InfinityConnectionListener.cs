using Infinity.Core;
using Infinity.Udp;
using Infinity.WebSockets;
using System.Net;

namespace Infinity.Multiplexer
{
    public class InfinityConnectionListener : NetworkConnectionListener
    {
        public override double AveragePing => udp_listener.AveragePing;

        public override int ConnectionCount => udp_listener.ConnectionCount + ws_listener.ConnectionCount;

        private UdpConnectionListener udp_listener;
        private WebSocketConnectionListener ws_listener;

        public InfinityConnectionListener(IPEndPoint endPoint, ILogger logger = null)
        {
            udp_listener = new UdpConnectionListener(endPoint, IPMode.IPv4, logger);
            ws_listener = new WebSocketConnectionListener(endPoint, logger);

            udp_listener.HandshakeConnection = HandshakeConnection;
            ws_listener.HandshakeConnection = HandshakeConnection;

            udp_listener.NewConnection += (e) =>
            {
                InvokeNewConnection(e.Connection, e.HandshakeData);
            };

            ws_listener.NewConnection += (e) =>
            {
                InvokeNewConnection(e.Connection, e.HandshakeData);
            };
        }

        public override void Start()
        {
            udp_listener.Start();
            ws_listener.Start();
        }

        protected override void Dispose(bool disposing)
        {
            udp_listener.Dispose();
            ws_listener.Dispose();

            base.Dispose(disposing);
        }
    }
}