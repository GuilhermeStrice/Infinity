using System.Net;
using System.Net.Sockets;
using Infinity.Core;
using Infinity.Core.Exceptions;

namespace Infinity.WebSockets
{
    public class WebSocketServerConnection : WebSocketConnection
    {
        private readonly Socket socket;
        private readonly NetworkStream stream;

        protected override NetworkStream Stream => stream;
        protected override bool MaskOutgoingFrames => false;

        protected override bool ValidateIncomingMask() => true;
        public override int MaxPayloadSize { get; set; } = 64 * 1024 * 1024; // default 64MB

        // additional handshake metadata exposed for consumers
        public string? RequestPath { get; internal set; }
        public string? RequestQuery { get; internal set; }
        public IReadOnlyDictionary<string, string>? RequestHeaders { get; internal set; }
        public string? SelectedProtocol { get; internal set; }

        public WebSocketServerConnection(Socket socket, ILogger? logger)
        {
            this.socket = socket;
            this.stream = new NetworkStream(socket, ownsSocket: false);
            this.logger = logger;

            EndPoint = (IPEndPoint)socket.RemoteEndPoint!;
            IPMode = EndPoint.AddressFamily == AddressFamily.InterNetwork ? IPMode.IPv4 : IPMode.IPv6;
            State = ConnectionState.Connected;

            StartPingTimer(Timeout.Infinite);
        }

        public void Start() => _ = Task.Run(ReceiveLoop);

        protected override void Dispose(bool disposing)
        {
            shuttingDown = true;
            try { pingTimer?.Dispose(); } catch { }
            try { stream.Dispose(); } catch { }
            try { socket.Dispose(); } catch { }
            base.Dispose(disposing);
        }

        public override Task Connect(MessageWriter writer, int timeout = 5000)
        {
            throw new InfinityException("Server connection cannot initiate Connect()");
        }
    }
}
