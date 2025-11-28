	using System.Collections.Concurrent;
	using System.Net;
	using System.Net.Sockets;
	using System.Security.Cryptography;
	using System.Text;
	using Infinity.Core;
	using Infinity.Core.Exceptions;

	namespace Infinity.WebSockets
	{
		public class WebSocketConnectionListener : NetworkConnectionListener
		{
			private readonly TcpListener listener;
			private readonly ILogger? logger;
			private readonly CancellationTokenSource cts = new CancellationTokenSource();
			private readonly ConcurrentDictionary<IPEndPoint, WebSocketServerConnection> all_connections = new ConcurrentDictionary<IPEndPoint, WebSocketServerConnection>();

		// Optional subprotocol negotiation: given offered protocols, return the selected one or null for none
		public Func<IReadOnlyList<string>, string?>? ProtocolSelector { get; set; }

			public override double AveragePing => all_connections.Count == 0 ? 0 : all_connections.Values.Sum(c => c.AveragePingMs) / all_connections.Count;
			public override int ConnectionCount => all_connections.Count;

		public WebSocketConnectionListener(IPEndPoint _endpoint, ILogger? _logger = null)
		{
			EndPoint = _endpoint;
			IPMode = _endpoint.AddressFamily == AddressFamily.InterNetwork ? IPMode.IPv4 : IPMode.IPv6;
			listener = new TcpListener(_endpoint);
			logger = _logger;
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

			_ = Task.Run(AcceptLoop);
		}

		private async Task AcceptLoop()
		{
			while (!cts.IsCancellationRequested)
			{
				TcpClient? client = null;
				try
				{
					client = await listener.AcceptTcpClientAsync(cts.Token);
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					logger?.WriteError("Accept failed: " + ex.Message);
					continue;
				}

				_ = Task.Run(() => HandleClient(client));
			}
		}

		private async Task HandleClient(TcpClient _client)
		{
			var stream = new NetworkStream(_client.Client, ownsSocket: false);
			stream.ReadTimeout = 5000;
			stream.WriteTimeout = 5000;

			// Read HTTP request headers
			string request = await ReadHeaders(stream);
			if (string.IsNullOrEmpty(request) || !request.StartsWith("GET "))
			{
				await WriteHttpError(stream, 400, "Bad Request");
				_client.Close();
				return;
			}

			var headers = ParseHeaders(request);
			if (!ValidateUpgradeRequest(headers, out var secKey))
			{
				await WriteHttpError(stream, 400, "Bad WebSocket Upgrade");
				_client.Close();
				return;
			}

			// Optional user handshake gate using existing pattern
			if (HandshakeConnection != null)
			{
				var ep = (IPEndPoint)_client.Client.RemoteEndPoint!;
				var dummyReader = MessageReader.Get();
				dummyReader.Length = 0;
				if (!HandshakeConnection(ep, dummyReader, out var response))
				{
					if (response != null)
					{
						await stream.WriteAsync(response.Buffer, 0, response.Length);
					}
					_client.Close();
					dummyReader.Recycle();
					return;
				}
				dummyReader.Recycle();
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
			await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
			await stream.FlushAsync();

			var wsConn = new WebSocketServerConnection(_client.Client, logger);
			all_connections.TryAdd(wsConn.EndPoint, wsConn);
			wsConn.Start();

			var handshakeReader = MessageReader.Get();
			handshakeReader.Length = 0;
			InvokeNewConnection(wsConn, handshakeReader);
			try { stream.Dispose(); } catch { }
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

		private static async Task WriteHttpError(NetworkStream stream, int code, string message)
		{
			string resp = $"HTTP/1.1 {code} {message}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
			var bytes = Encoding.ASCII.GetBytes(resp);
			try { await stream.WriteAsync(bytes, 0, bytes.Length); } catch { }
		}

		private static async Task<string> ReadHeaders(NetworkStream _stream)
		{
			var sb = new StringBuilder();
			byte[] buffer = new byte[1024];
			int matched = 0;
			while (true)
			{
				int read = await _stream.ReadAsync(buffer, 0, buffer.Length);
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

		private static string? ExtractHeader(string _headers, string _name)
		{
			foreach (var line in _headers.Split("\r\n"))
			{
				int idx = line.IndexOf(':');
				if (idx <= 0) continue;
				var key = line.Substring(0, idx).Trim();
				if (key.Equals(_name, StringComparison.OrdinalIgnoreCase))
				{
					return line.Substring(idx + 1).Trim();
				}
			}
			return null;
		}

		private static string ComputeWebSocketAccept(string _clientKey)
		{
			string concat = _clientKey + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
			byte[] sha1 = SHA1.HashData(Encoding.ASCII.GetBytes(concat));
			return Convert.ToBase64String(sha1);
		}

		internal void RemoveConnection(IPEndPoint _endpoint)
		{
			all_connections.TryRemove(_endpoint, out _);
		}

		protected override void Dispose(bool _disposing)
		{
			try { cts.Cancel(); } catch { }
			try { listener.Stop(); } catch { }
			foreach (var c in all_connections.Values)
			{
				try { c.Dispose(); } catch { }
			}
			all_connections.Clear();
			base.Dispose(_disposing);
		}
	}
}


