using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Infinity.Core;
using Infinity.Core.Exceptions;

namespace Infinity.WebSockets
{
    public class WebSocketClientConnection : WebSocketConnection
    {
        private TcpClient? client;
        private NetworkStream? stream;
        private string? host, path;
        private int port;
        private string? lastSecKey;

        public string? RequestedProtocol { get; set; }
        public string? AcceptedProtocol { get; private set; }

        protected override NetworkStream Stream => stream!;
        protected override bool MaskOutgoingFrames => true;

        protected override bool ValidateIncomingMask(bool masked) => !masked;
        public override int MaxPayloadSize { get; set; } = Configuration.MaxBufferSize;

        public WebSocketClientConnection(ILogger? _logger = null) { logger = _logger; }

        public override async Task Connect(MessageWriter writer, int timeout = 5000)
        {
            var reader = writer.ToReader();
            string url = reader.ReadString();
            reader.Recycle();

            var uri = new Uri(url);
            host = uri.Host;
            port = uri.Port;
            path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

            client = new TcpClient { NoDelay = true };
            var cts = new CancellationTokenSource(timeout);
            await client.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
            stream = client.GetStream();

            lastSecKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

            string request =
                $"GET {path} HTTP/1.1\r\n" +
                $"Host: {host}:{port}\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Key: {lastSecKey}\r\n" +
                "Sec-WebSocket-Version: 13\r\n" +
                (RequestedProtocol != null ? $"Sec-WebSocket-Protocol: {RequestedProtocol}\r\n" : string.Empty) +
                "\r\n";

            byte[] reqBytes = Encoding.ASCII.GetBytes(request);
            await stream.WriteAsync(reqBytes, 0, reqBytes.Length).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);

            string headers = await ReadHeaders(stream).ConfigureAwait(false);
            if (!headers.StartsWith("HTTP/1.1 101"))
                throw new InfinityException("WebSocket upgrade failed: " + headers.Split('\r')[0]);

            var dict = ParseHeaders(headers);
            if (!dict.TryGetValue("Upgrade", out var up) || !up.Contains("websocket", StringComparison.OrdinalIgnoreCase))
                throw new InfinityException("WebSocket upgrade failed: missing/invalid Upgrade header");
            if (!dict.TryGetValue("Connection", out var conn) || !conn.Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
                throw new InfinityException("WebSocket upgrade failed: missing/invalid Connection header");
            if (!dict.TryGetValue("Sec-WebSocket-Accept", out var accept) || string.IsNullOrWhiteSpace(accept))
                throw new InfinityException("WebSocket upgrade failed: missing Sec-WebSocket-Accept");

            string expected = ComputeWebSocketAccept(lastSecKey!);
            if (!string.Equals(accept.Trim(), expected, StringComparison.Ordinal))
                throw new InfinityException("WebSocket upgrade failed: bad Sec-WebSocket-Accept");

            if (dict.TryGetValue("Sec-WebSocket-Protocol", out var acceptedProtocol))
                AcceptedProtocol = acceptedProtocol.Trim();

            EndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
            IPMode = EndPoint.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? IPMode.IPv4 : IPMode.IPv6;
            State = ConnectionState.Connected;

            StartPingTimer();
            _ = Task.Run(ReceiveLoop);
        }

        protected override void Dispose(bool disposing)
        {
            shuttingDown = true;
            try { pingTimer?.Dispose(); } catch { }
            try { stream?.Dispose(); } catch { }
            try { client?.Dispose(); } catch { }
            base.Dispose(disposing);
        }
    }
}
