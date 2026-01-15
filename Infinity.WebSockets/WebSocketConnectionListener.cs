using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class WebSocketConnectionListener : NetworkConnectionListener
    {
        private ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

        private readonly TcpListener listener;
        private readonly ILogger? logger;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        private readonly ConcurrentDictionary<Guid, WebSocketServerConnection> connectionsById = new ConcurrentDictionary<Guid, WebSocketServerConnection>();
        private readonly ConcurrentDictionary<IPEndPoint, Guid> idByEndPoint = new ConcurrentDictionary<IPEndPoint, Guid>();

        public Func<IReadOnlyList<string>, string?>? ProtocolSelector { get; set; }

        // Protect against very large headers
        public int MaxHeaderBytes { get; set; } = 64 * 1024; // 64KB default
        // Timeout (ms) for each individual ReadAsync during handshake to avoid slowloris
        public int HandshakeReadTimeoutMs { get; set; } = 5000;

        public override double AveragePing
            => connectionsById.Count == 0 ? 0 : connectionsById.Values.Sum(c => c.AveragePingMs) / connectionsById.Count;

        public override int ConnectionCount => connectionsById.Count;

        public WebSocketConnectionListener(IPEndPoint endpoint, ILogger? logger = null)
        {
            EndPoint = endpoint;
            IPMode = endpoint.AddressFamily == AddressFamily.InterNetwork ? IPMode.IPv4 : IPMode.IPv6;
            listener = new TcpListener(endpoint);
            this.logger = logger;
        }

        public override void Start()
        {
            try
            {
                listener.Start();
            }
            catch (SocketException e)
            {
                throw new InfinityException("Could not start TCP listener", e);
            }

            _ = Task.Run(() => AcceptLoopAsync(cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    TcpClient? client = null;
                    try
                    {
                        client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { break; }
                    catch (ObjectDisposedException) { break; }
                    catch (Exception ex)
                    {
                        logger?.WriteError("Accept failed: " + ex.Message);
                        continue;
                    }

                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
            }
            catch (Exception ex)
            {
                logger?.WriteError("AcceptLoop fatal: " + ex.Message);
            }
        }

        internal void RemoveConnection(IPEndPoint endpoint)
        {
            if (endpoint == null) return;
            if (idByEndPoint.TryRemove(endpoint, out var id))
            {
                connectionsById.TryRemove(id, out _);
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken token)
        {
            NetworkStream handshakeStream = null!;
            try
            {
                handshakeStream = new NetworkStream(tcpClient.Client, ownsSocket: false);
                handshakeStream.ReadTimeout = Timeout.Infinite;
                handshakeStream.WriteTimeout = Timeout.Infinite;

                string request;
                try
                {
                    request = await ReadHeadersAsync(handshakeStream, MaxHeaderBytes, HandshakeReadTimeoutMs, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    tcpClient.Close();
                    return;
                }
                catch (IOException)
                {
                    tcpClient.Close();
                    return;
                }

                if (string.IsNullOrEmpty(request))
                {
                    await WriteHttpErrorAsync(handshakeStream, 400, "Bad Request").ConfigureAwait(false);
                    tcpClient.Close();
                    return;
                }

                // Parse request-line and headers
                var lines = request.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0 || !lines[0].StartsWith("GET ", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteHttpErrorAsync(handshakeStream, 400, "Bad Request").ConfigureAwait(false);
                    tcpClient.Close();
                    return;
                }

                // Extract path and query from request-line
                // e.g. "GET /path?query=1 HTTP/1.1"
                var requestLineParts = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string requestPath = "/";
                string requestQuery = string.Empty;
                if (requestLineParts.Length >= 2)
                {
                    var uriPart = requestLineParts[1];
                    int qidx = uriPart.IndexOf('?');
                    if (qidx >= 0)
                    {
                        requestPath = Uri.UnescapeDataString(uriPart.Substring(0, qidx));
                        requestQuery = uriPart.Substring(qidx + 1);
                    }
                    else
                    {
                        requestPath = Uri.UnescapeDataString(uriPart);
                    }
                }

                var headers = ParseHeaders(request);
                if (!ValidateUpgradeRequest(headers, out var secKey))
                {
                    await WriteHttpErrorAsync(handshakeStream, 400, "Bad WebSocket Upgrade").ConfigureAwait(false);
                    tcpClient.Close();
                    return;
                }

                // Host required (stronger validation)
                if (!headers.TryGetValue("Host", out var host) || string.IsNullOrWhiteSpace(host))
                {
                    await WriteHttpErrorAsync(handshakeStream, 400, "Missing Host").ConfigureAwait(false);
                    tcpClient.Close();
                    return;
                }

                // Optional origin check hook (if you want)
                // if (headers.TryGetValue("Origin", out var origin) && !IsOriginAllowed(origin)) { ... }

                // Optional user handshake gate
                if (HandshakeConnection != null)
                {
                    var ep = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                    var dummyReader = new MessageReader(allocator);
                    dummyReader.Length = 0;
                    if (!HandshakeConnection(ep, dummyReader, out var response))
                    {
                        using var manager = response.AsManager();
                        await handshakeStream.WriteAsync(manager.Memory, token).ConfigureAwait(false);
                        tcpClient.Close();
                        return;
                    }
                }

                string accept = ComputeWebSocketAccept(secKey);
                string? selectedProtocol = null;
                if (headers.TryGetValue("Sec-WebSocket-Protocol", out var offered) && ProtocolSelector != null)
                {
                    var list = offered.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    selectedProtocol = ProtocolSelector.Invoke(list);
                }

                string responseHeaders =
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {accept}\r\n" +
                    (selectedProtocol != null ? $"Sec-WebSocket-Protocol: {selectedProtocol}\r\n" : string.Empty) +
                    "\r\n";

                byte[] responseBytes = Encoding.ASCII.GetBytes(responseHeaders);
                try
                {
                    await handshakeStream.WriteAsync(responseBytes, 0, responseBytes.Length, token).ConfigureAwait(false);
                    await handshakeStream.FlushAsync(token).ConfigureAwait(false);
                }
                catch
                {
                    tcpClient.Close();
                    return;
                }

                // create server connection using underlying socket
                var wsConn = new WebSocketServerConnection(tcpClient.Client, logger)
                {
                    RequestPath = requestPath,
                    RequestQuery = requestQuery,
                    RequestHeaders = headers,
                    SelectedProtocol = selectedProtocol
                };

                var id = Guid.NewGuid();
                if (wsConn.EndPoint == null)
                {
                    try { wsConn.Dispose(); } catch { }
                    tcpClient.Close();
                    return;
                }

                connectionsById.TryAdd(id, wsConn);
                idByEndPoint.TryAdd(wsConn.EndPoint, id);

                // Hook up removal on disposal: ensure listener cleans up when connection is disposed
                _ = Task.Run(() => WaitForConnectionDisposeAsync(id, wsConn));

                wsConn.Start();

                var handshakeReader = new MessageReader(allocator);
                handshakeReader.Length = 0;
                InvokeNewConnection(wsConn, handshakeReader);

                // Do not dispose handshakeStream - the socket is now owned by wsConn
            }
            catch (Exception ex)
            {
                logger?.WriteError("HandleClient failed: " + ex.Message);
                try { tcpClient.Close(); } catch { }
            }
        }

        private async Task WaitForConnectionDisposeAsync(Guid id, WebSocketServerConnection conn)
        {
            try
            {
                // Poll until connection state is NotConnected or object disposed
                while (conn.State == ConnectionState.Connected)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                connectionsById.TryRemove(id, out _);
                if (conn.EndPoint != null) idByEndPoint.TryRemove(conn.EndPoint, out _);
            }
        }

        private static async Task WriteHttpErrorAsync(NetworkStream stream, int code, string message)
        {
            string resp = $"HTTP/1.1 {code} {message}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
            var bytes = Encoding.ASCII.GetBytes(resp);
            try { await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false); } catch { }
            try { await stream.FlushAsync().ConfigureAwait(false); } catch { }
        }

        private static Dictionary<string, string> ParseHeaders(string raw)
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

        private static bool ValidateUpgradeRequest(Dictionary<string, string> headers, out string secKey)
        {
            secKey = string.Empty;
            if (!headers.TryGetValue("Upgrade", out var upgrade) || !upgrade.Contains("websocket", StringComparison.OrdinalIgnoreCase)) return false;
            if (!headers.TryGetValue("Connection", out var connection) || !connection.Contains("Upgrade", StringComparison.OrdinalIgnoreCase)) return false;
            if (!headers.TryGetValue("Sec-WebSocket-Version", out var version) || version.Trim() != "13") return false;
            if (!headers.TryGetValue("Sec-WebSocket-Key", out secKey) || string.IsNullOrWhiteSpace(secKey)) return false;

            try
            {
                var keyBytes = Convert.FromBase64String(secKey);
                if (keyBytes.Length != 16) return false;
            }
            catch { return false; }

            return true;
        }

        // Read headers up to maxHeaderBytes; each ReadAsync has a timeout to protect against slowloris.
        private static async Task<string> ReadHeadersAsync(NetworkStream stream, int maxHeaderBytes, int perReadTimeoutMs, CancellationToken token)
        {
            var sb = new StringBuilder();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                int matched = 0;
                int total = 0;
                while (!token.IsCancellationRequested)
                {
                    // per-read timeout token
                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    if (perReadTimeoutMs > 0) readCts.CancelAfter(perReadTimeoutMs);

                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length, readCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        if (token.IsCancellationRequested) throw;
                        // per-read timeout expired -> treat as handshake failure
                        throw new IOException("Handshake read timeout");
                    }
                    catch
                    {
                        return string.Empty;
                    }

                    if (read <= 0) break;
                    total += read;
                    if (total > maxHeaderBytes)
                    {
                        // header too big
                        return string.Empty;
                    }

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
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static string ComputeWebSocketAccept(string clientKey)
        {
            string concat = clientKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            byte[] sha1 = SHA1.HashData(Encoding.ASCII.GetBytes(concat));
            return Convert.ToBase64String(sha1);
        }

        protected override void Dispose(bool disposing)
        {
            try { cts.Cancel(); } catch { }
            try { listener.Stop(); } catch { }

            foreach (var kv in connectionsById)
            {
                try { kv.Value.Dispose(); } catch { }
            }
            connectionsById.Clear();
            idByEndPoint.Clear();

            base.Dispose(disposing);
        }
    }
}
