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

        // Utility methods
        protected static string ComputeWebSocketAccept(string clientKey)
        {
            string concat = clientKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] sha1 = System.Security.Cryptography.SHA1.HashData(Encoding.ASCII.GetBytes(concat));
            return Convert.ToBase64String(sha1);
        }

        protected static Dictionary<string, string> ParseHeaders(string raw)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = raw.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                int idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var k = line[..idx].Trim();
                var v = line[(idx + 1)..].Trim();
                dict[k] = v;
            }
            return dict;
        }

        protected static async Task<string> ReadHeaders(NetworkStream stream)
        {
            var sb = new StringBuilder();
            byte[] buffer = new byte[1024];
            int matched = 0;
            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0) break;
                sb.Append(Encoding.ASCII.GetString(buffer, 0, read));
                for (int i = 0; i < read; i++)
                {
                    char c = (char)buffer[i];
                    if ((matched == 0 || matched == 2) && c == '\r') matched++;
                    else if ((matched == 1 || matched == 3) && c == '\n') matched++;
                    else matched = 0;
                    if (matched == 4) return sb.ToString();
                }
            }
            return sb.ToString();
        }

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
