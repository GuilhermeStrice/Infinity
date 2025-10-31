using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Infinity.Core;
using Infinity.Core.Exceptions;
using Infinity.WebSockets.Enums;

namespace Infinity.WebSockets
{
	public class WebSocketClientConnection : NetworkConnection
	{
		private readonly ILogger? logger;
		private TcpClient? client;
		private NetworkStream? stream;
		private string? host;
		private int port;
		private string? path;
		private string? lastSecKey;

		public string? RequestedProtocol { get; set; }
		public string? AcceptedProtocol { get; private set; }

		public int MaxMessageSize { get; set; } = 64 * 1024 * 1024;

		private Timer? pingTimer;
		private long lastPingTicks;
		private volatile bool shuttingDown;
		private volatile bool closeSent;
		private volatile bool closeReceived;

		public WebSocketClientConnection(ILogger? _logger = null)
		{
			logger = _logger;
		}

		public override async Task Connect(MessageWriter _writer, int _timeout = 5000)
		{
			// Expect a URL string encoded using MessageWriter.Write(string)
			var reader = _writer.ToReader();
			string url = reader.ReadString();
			reader.Recycle();
			var uri = new Uri(url);
			host = uri.Host;
			port = uri.Port;
			path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

			client = new TcpClient();
			client.NoDelay = true;
			var cts = new CancellationTokenSource(_timeout);
			await client.ConnectAsync(host, port, cts.Token);
			stream = client.GetStream();

			string secKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
			lastSecKey = secKey;
			string request =
				$"GET {path} HTTP/1.1\r\n" +
				$"Host: {host}:{port}\r\n" +
				"Upgrade: websocket\r\n" +
				"Connection: Upgrade\r\n" +
				$"Sec-WebSocket-Key: {secKey}\r\n" +
				"Sec-WebSocket-Version: 13\r\n" +
				(RequestedProtocol != null ? $"Sec-WebSocket-Protocol: {RequestedProtocol}\r\n" : string.Empty) +
				"\r\n";

			byte[] reqBytes = Encoding.ASCII.GetBytes(request);
			await stream.WriteAsync(reqBytes, 0, reqBytes.Length);
			await stream.FlushAsync();

			// read response
			string headers = await ReadHeaders(stream);
			if (!headers.StartsWith("HTTP/1.1 101"))
			{
				throw new InfinityException("WebSocket upgrade failed: " + headers.Split('\r')[0]);
			}

			var dict = ParseHeaders(headers);
			if (!dict.TryGetValue("Upgrade", out var up) || !up.Contains("websocket", StringComparison.OrdinalIgnoreCase))
			{
				throw new InfinityException("WebSocket upgrade failed: missing/invalid Upgrade header");
			}
			if (!dict.TryGetValue("Connection", out var conn) || !conn.Contains("Upgrade", StringComparison.OrdinalIgnoreCase))
			{
				throw new InfinityException("WebSocket upgrade failed: missing/invalid Connection header");
			}
			if (!dict.TryGetValue("Sec-WebSocket-Accept", out var accept) || string.IsNullOrWhiteSpace(accept))
			{
				throw new InfinityException("WebSocket upgrade failed: missing Sec-WebSocket-Accept");
			}
			string expected = ComputeWebSocketAccept(lastSecKey!);
			if (!string.Equals(accept.Trim(), expected, StringComparison.Ordinal))
			{
				throw new InfinityException("WebSocket upgrade failed: bad Sec-WebSocket-Accept");
			}
			if (dict.TryGetValue("Sec-WebSocket-Protocol", out var acceptedProtocol))
			{
				AcceptedProtocol = acceptedProtocol.Trim();
			}

			EndPoint = (IPEndPoint)client.Client.RemoteEndPoint!;
			IPMode = EndPoint.AddressFamily == AddressFamily.InterNetwork ? IPMode.IPv4 : IPMode.IPv6;
			State = ConnectionState.Connected;

			pingTimer = new Timer(SendPing, null, 5000, Timeout.Infinite);

			_ = Task.Run(ReceiveLoop);
		}

		public override async Task<SendErrors> Send(MessageWriter message)
		{
			// Check if the connection is ready
			if (state != ConnectionState.Connected || stream == null || closeSent)
				return SendErrors.Disconnected;

			// Allow any pre-send processing
			InvokeBeforeSend(message);

			// Create a WebSocket frame from the message
			var frame = WebSocketFrame.CreateFrame(
				message.Buffer.AsSpan(0, message.Length),
				message.Length,
				WebSocketOpcode.Binary,
				true, // fin
				true // mask
			);

			try
			{
				// Write the frame to the network stream
				await stream.WriteAsync(frame.Buffer, 0, frame.Length);
				await stream.FlushAsync();
			}
			catch (Exception ex)
			{
				logger?.WriteError("WebSocket send failed: " + ex.Message);
				DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Send failed");
				return SendErrors.Disconnected;
			}
			finally
			{
				// Always recycle the frame to avoid memory leaks
				frame.Recycle();
			}

			return SendErrors.None;
		}

		protected override void DisconnectRemote(string _reason, MessageReader _reader)
		{
			MessageWriter frame = null;
			try
			{
				if (stream != null)
				{
					frame = WebSocketFrame.CreateFrame(ReadOnlySpan<byte>.Empty, 0, WebSocketOpcode.Close, true, true);
					stream.Write(frame.Buffer, 0, frame.Length);
				}
			}
			catch { }
			finally
			{
				frame?.Recycle();
				InvokeDisconnected(_reason, _reader);
				Dispose();
			}
		}

		protected override void DisconnectInternal(InfinityInternalErrors _error, string _reason)
		{
			OnInternalDisconnect?.Invoke(_error)?.ToReader()?.Recycle();
			State = ConnectionState.NotConnected;
			InvokeDisconnected(_reason, null);
			Dispose();
		}

		protected override bool SendDisconnect(MessageWriter _writer)
		{
			MessageWriter frame = null;
			try
			{
				if (stream != null)
				{
					frame = WebSocketFrame.CreateFrame(ReadOnlySpan<byte>.Empty, 0, WebSocketOpcode.Close, true, true);
					stream.Write(frame.Buffer, 0, frame.Length);
				}
				frame?.Recycle();
				return true;
			}
			catch
			{
				frame?.Recycle();
				return false;
			}
		}

		protected override void SetState(ConnectionState _state)
		{
			state = _state;
		}

		private async Task ReceiveLoop()
		{
			if (stream == null) return;
			try
			{
				List<byte>? frag = null;
				WebSocketOpcode fragOpcode = WebSocketOpcode.Binary;
				int totalPayloadLen = 0;
				while (state == ConnectionState.Connected)
				{
				if (!WebSocketFrame.TryReadFrame(stream, out var opcode, out var fin, out var masked, out var payload))
					{
						DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Failed to read frame");
						return;
					}

				if (closeReceived)
				{
					if (opcode != WebSocketOpcode.Close && opcode != WebSocketOpcode.Ping && opcode != WebSocketOpcode.Pong)
					{
						continue;
					}
				}

					// Masking enforcement: server frames must NOT be masked
					if (masked)
					{
						var cw = MessageWriter.Get();
						cw.Write((byte)(1002 >> 8)); cw.Write((byte)(1002 & 0xFF));
						var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, true);
						await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
						frameClose.Recycle(); cw.Recycle();
						DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Masked server frame");
						return;
					}

					// Fragmentation handling
					if (!fin)
					{
						if (opcode == WebSocketOpcode.Binary || opcode == WebSocketOpcode.Text)
						{
							frag = frag ?? new List<byte>(payload.Length * 2);
							frag.Clear();
							frag.AddRange(payload);
							fragOpcode = opcode;
							totalPayloadLen = payload.Length;
						}
						else if (opcode == WebSocketOpcode.Continuation && frag != null)
						{
							frag.AddRange(payload);
							totalPayloadLen += payload.Length;
						}

						if (totalPayloadLen > MaxMessageSize)
						{
							var cw = MessageWriter.Get();
							cw.Write((byte)(1009 >> 8)); cw.Write((byte)(1009 & 0xFF));
							var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, true);
							await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
							frameClose.Recycle(); cw.Recycle();
							DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Message too big");
							return;
						}
						continue;
					}

					if (opcode == WebSocketOpcode.Continuation && frag != null)
					{
						frag.AddRange(payload);
						totalPayloadLen += payload.Length;
						if (totalPayloadLen > MaxMessageSize)
						{
							var cw = MessageWriter.Get();
							cw.Write((byte)(1009 >> 8)); cw.Write((byte)(1009 & 0xFF));
							var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, true);
							await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
							frameClose.Recycle(); cw.Recycle();
							DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Message too big");
							return;
						}
						payload = frag.ToArray();
						opcode = fragOpcode;
						frag = null;
					}

					if (payload.Length > MaxMessageSize)
					{
						var cw = MessageWriter.Get();
						cw.Write((byte)(1009 >> 8)); cw.Write((byte)(1009 & 0xFF));
						var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, true);
						await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
						frameClose.Recycle(); cw.Recycle();
						DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Message too big");
						return;
					}

					switch (opcode)
					{
						case WebSocketOpcode.Binary:
						case WebSocketOpcode.Continuation:
						{
							var reader = MessageReader.Get(payload, 0, payload.Length);
							InvokeBeforeReceive(reader);
							InvokeDataReceived(reader);
							break;
						}
						case WebSocketOpcode.Text:
						{
							// Validate UTF-8; send 1007 close on invalid
							try
							{
								var _ = new UTF8Encoding(false, true).GetString(payload);
							}
							catch (DecoderFallbackException)
							{
								var cw = MessageWriter.Get();
								cw.Write((byte)(1007 >> 8)); cw.Write((byte)(1007 & 0xFF));
								var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, true);
								await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
								frameClose.Recycle(); cw.Recycle();
								DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Invalid UTF-8");
								return;
							}
							var reader = MessageReader.Get(payload, 0, payload.Length);
							InvokeBeforeReceive(reader);
							InvokeDataReceived(reader);
							break;
						}
						case WebSocketOpcode.Ping:
						{
							MessageWriter pong = WebSocketFrame.CreateFrame(payload.AsSpan(), payload.Length, WebSocketOpcode.Pong, true, true);
							await stream.WriteAsync(pong.Buffer, 0, pong.Length);
							await stream.FlushAsync();
							pong.Recycle();
							break;
						}
						case WebSocketOpcode.Pong:
						{
							if (lastPingTicks != 0)
							{
								var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastPingTicks).TotalMilliseconds;
								AveragePingMs = (float)elapsed;
								lastPingTicks = 0;
							}
							break;
						}
						case WebSocketOpcode.Close:
						{
							ushort code = 1000;
							string reason = string.Empty;
							if (payload.Length >= 2)
							{
								code = (ushort)((payload[0] << 8) | payload[1]);
								if (payload.Length > 2)
								{
									try { reason = Encoding.UTF8.GetString(payload, 2, payload.Length - 2); } catch { }
								}
							}
						if (!closeSent)
						{
							var closeWriter = MessageWriter.Get();
							if (payload.Length >= 2)
							{
								closeWriter.Write((byte)(code >> 8));
								closeWriter.Write((byte)(code & 0xFF));
								if (!string.IsNullOrEmpty(reason))
								{
									var rb = Encoding.UTF8.GetBytes(reason);
									closeWriter.Write(rb, rb.Length);
								}
							}
							var frame = WebSocketFrame.CreateFrame(closeWriter.Buffer.AsSpan(0, closeWriter.Length), closeWriter.Length, WebSocketOpcode.Close, true, true);
							await stream.WriteAsync(frame.Buffer, 0, frame.Length);
							frame.Recycle(); closeWriter.Recycle();
							closeSent = true;
						}
						closeReceived = true;
							DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, string.IsNullOrEmpty(reason) ? "Remote closed" : reason);
							return;
						}
						default:
						{
							break;
						}
					}
				}
			}
			catch (IOException)
			{
				return;
			}
			catch (SocketException)
			{
				return;
			}
			catch (Exception ex)
			{
				if (shuttingDown || state != ConnectionState.Connected)
				{
					return;
				}
				logger?.WriteError("WebSocket client receive loop failed: " + ex.Message);
				DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Receive failed");
			}
		}

		private void SendPing(object? _)
		{
			if (state != ConnectionState.Connected || stream == null) return;
			MessageWriter frame = null;
			try
			{
				lastPingTicks = DateTime.UtcNow.Ticks;
				frame = WebSocketFrame.CreateFrame(ReadOnlySpan<byte>.Empty, 0, WebSocketOpcode.Ping, true, true);
				stream.Write(frame.Buffer, 0, frame.Length);
			}
			catch { }
			finally
			{
				frame?.Recycle();
				try { pingTimer?.Change(5000, Timeout.Infinite); } catch { }
			}
		}

		protected override void Dispose(bool _disposing)
		{
			shuttingDown = true;
			try { pingTimer?.Dispose(); } catch { }
			try { stream?.Dispose(); } catch { }
			try { client?.Dispose(); } catch { }
			base.Dispose(_disposing);
		}

		private static async Task<string> ReadHeaders(NetworkStream stream)
		{
			var builder = new StringBuilder();
			byte[] buffer = new byte[1024];
			int consecutiveMatch = 0; // Tracks \r\n\r\n sequence

			while (true)
			{
				int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
				if (bytesRead <= 0) break; // End of stream

				// Append the chunk to the string builder
				builder.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));

				// Check for end-of-headers sequence (\r\n\r\n)
				for (int i = 0; i < bytesRead; i++)
				{
					char c = (char)buffer[i];

					if ((consecutiveMatch == 0 || consecutiveMatch == 2) && c == '\r')
						consecutiveMatch++;
					else if ((consecutiveMatch == 1 || consecutiveMatch == 3) && c == '\n')
						consecutiveMatch++;
					else
						consecutiveMatch = 0;

					// End of headers found
					if (consecutiveMatch == 4)
						return builder.ToString();
				}
			}

			return builder.ToString();
		}

		private static Dictionary<string, string> ParseHeaders(string rawHeaders)
		{
			// Use case-insensitive dictionary for HTTP headers
			var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

			// Split the raw header string into lines, ignoring empty lines
			var lines = rawHeaders.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

			// Start from index 1 assuming the first line is the request/status line
			for (int i = 1; i < lines.Length; i++)
			{
				var line = lines[i];

				// Find the first colon, which separates key and value
				int colonIndex = line.IndexOf(':');
				if (colonIndex <= 0) continue; // Skip malformed lines

				// Extract key and value, trimming whitespace
				var key = line[..colonIndex].Trim();
				var value = line[(colonIndex + 1)..].Trim();

				headers[key] = value;
			}

			return headers;
		}

		private static string ComputeWebSocketAccept(string clientKey)
		{
			const string WebSocketGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
			var concatenated = clientKey + WebSocketGuid;
			var hash = SHA1.HashData(Encoding.ASCII.GetBytes(concatenated));
			return Convert.ToBase64String(hash);
		}
	}
}


