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
        private readonly TcpListener listener;
        private readonly ILogger? logger;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        // Use a GUID -> connection mapping to avoid collisions and reuse issues with IPEndPoint
        private readonly ConcurrentDictionary<Guid, WebSocketServerConnection> connectionsById = new ConcurrentDictionary<Guid, WebSocketServerConnection>();
        // Reverse map to allow RemoveConnection(IPEndPoint)
        private readonly ConcurrentDictionary<IPEndPoint, Guid> idByEndPoint = new ConcurrentDictionary<IPEndPoint, Guid>();

        // Optional subprotocol negotiation: given offered protocols, return the selected one or null for none
        public Func<IReadOnlyList<string>, string?>? ProtocolSelector { get; set; }

        // maximum headers to accept (protects against header flood). Adjustable if needed.
        public int MaxHeaderBytes { get; set; } = 64 * 1024; // 64 KB

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

            // Start accepting loop (fire-and-forget)
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

                    // Fire-and-forget handling; pass the token so the handler can observe shutdown
                    _ = Task.Run(() => HandleClientAsync(client, token), token);
                }
            }
            catch (Exception ex)
            {
                logger?.WriteError("AcceptLoop fatal: " + ex.Message);
            }
        }

        // Public RemoveConnection kept for compatibility with existing code
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
            // We will use a temporary NetworkStream for the handshake but we will not Dispose it here:
            // the underlying socket will be passed to WebSocketServerConnection which will manage lifetime.
            NetworkStream handshakeStream = null!;
            try
            {
                // Create a NetworkStream for the handshake with infinite timeouts (we'll enforce a read loop limit)
                handshakeStream = new NetworkStream(tcpClient.Client, ownsSocket: false);
                handshakeStream.ReadTimeout = Timeout.Infinite;
                handshakeStream.WriteTimeout = Timeout.Infinite;

                // Read the HTTP request headers; pass token and max header limit
                string request;
                try
                {
                    request = await ReadHeadersAsync(handshakeStream, MaxHeaderBytes, token).ConfigureAwait(false);
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

                if (string.IsNullOrEmpty(request) || !request.StartsWith("GET ", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteHttpErrorAsync(handshakeStream, 400, "Bad Request").ConfigureAwait(false);
                    tcpClient.Close();
                    return;
                }

                var headers = ParseHeaders(request);
                if (!ValidateUpgradeRequest(headers, out var secKey))
                {
                    await WriteHttpErrorAsync(handshakeStream, 400, "Bad WebSocket Upgrade").ConfigureAwait(false);
                    tcpClient.Close();
                    return;
                }

                // Optional user handshake gate using existing pattern
                if (HandshakeConnection != null)
                {
                    var ep = (IPEndPoint)tcpClient.Client.RemoteEndPoint!;
                    var dummyReader = MessageReader.Get();
                    try
                    {
                        dummyReader.Length = 0;
                        if (!HandshakeConnection(ep, dummyReader, out var response))
                        {
                            if (response != null)
                            {
                                try { await handshakeStream.WriteAsync(response.Buffer, 0, response.Length, token).ConfigureAwait(false); } catch { }
                                response.Recycle();
                            }
                            tcpClient.Close();
                            return;
                        }
                    }
                    finally
                    {
                        dummyReader.Recycle();
                    }
                }

                // Compute accept and optional subprotocol
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

                // create server connection using underlying socket (handshakeStream not disposed here)
                var wsConn = new WebSocketServerConnection(tcpClient.Client, logger);

                // Add to dictionaries
                var id = Guid.NewGuid();
                if (wsConn.EndPoint == null)
                {
                    // defensive: if no remote endpoint, close
                    try { wsConn.Dispose(); } catch { }
                    tcpClient.Close();
                    return;
                }

                // store mappings
                connectionsById.TryAdd(id, wsConn);
                idByEndPoint.TryAdd(wsConn.EndPoint, id);

                // Start receive loop and ping timer
                wsConn.Start();

                // Fire the NewConnection event (handshakeReader zero-length message per prior pattern)
                var handshakeReader = MessageReader.Get();
                handshakeReader.Length = 0;
                InvokeNewConnection(wsConn, handshakeReader);

                // Do NOT dispose handshakeStream here. The server connection owns the socket from now on.
                // Return from handler; connection continues in wsConn's ReceiveLoop
            }
            catch (Exception ex)
            {
                logger?.WriteError("HandleClient failed: " + ex.Message);
                try { tcpClient.Close(); } catch { }
            }
            finally
            {
                // we intentionally do NOT dispose the handshake stream here, because the underlying Socket
                // is now owned by the WebSocketServerConnection (which will Dispose the socket when appropriate).
                // However, if we created handshakeStream and the handshake failed and we are going to close the socket,
                // ensure it's closed:
                // The code above closes tcpClient when appropriate; no extra action needed here.
            }
        }

        // Read HTTP headers until \r\n\r\n or until maxBytes exceeded. Throws on OperationCanceledException if token cancels.
        private static async Task<string> ReadHeadersAsync(NetworkStream stream, int maxHeaderBytes, CancellationToken token)
        {
            var sb = new StringBuilder();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(1024);
            try
            {
                int matched = 0;
                int total = 0;
                while (!token.IsCancellationRequested)
                {
                    int read;
                    try
                    {
                        read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch
                    {
                        // treat as client closed/failed
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

            // Dispose and clear connections
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
