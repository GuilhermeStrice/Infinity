using System.Net;
using System.Net.Sockets;
using System.Text;
using Infinity.Core;
using Infinity.Core.Exceptions;
using Infinity.WebSockets.Enums;

namespace Infinity.WebSockets
{
	public class WebSocketServerConnection : WebSocketConnection
	{
		private readonly Socket socket;
		private readonly NetworkStream stream;

		public int MaxMessageSize { get; set; } = 64 * 1024 * 1024; // 64MB default

		public WebSocketServerConnection(Socket _socket, ILogger? _logger)
		{
			socket = _socket;
			stream = new NetworkStream(socket, ownsSocket: false);
			logger = _logger;
			EndPoint = (IPEndPoint)socket.RemoteEndPoint!;
			IPMode = EndPoint.AddressFamily == AddressFamily.InterNetwork ? IPMode.IPv4 : IPMode.IPv6;
			State = ConnectionState.Connected;

			pingTimer = new Timer(SendPing, null, Timeout.Infinite, Timeout.Infinite);
		}

		public void Start()
		{
			_ = Task.Run(ReceiveLoop);
			try { pingTimer?.Change(5000, Timeout.Infinite); } catch { }
		}

		public override async Task<SendErrors> Send(MessageWriter _writer)
		{
			if (state != ConnectionState.Connected || closeSent)
			{
				return SendErrors.Disconnected;
			}

			await InvokeBeforeSend(_writer).ConfigureAwait(false);
			MessageWriter frame = WebSocketFrame.CreateFrame(_writer.Buffer.AsSpan(0, _writer.Length), _writer.Length, WebSocketOpcode.Binary, _fin: true, _mask: false);
			try
			{
				await stream.WriteAsync(frame.Buffer, 0, frame.Length);
				await stream.FlushAsync();
			}
			catch (Exception ex)
			{
				logger?.WriteError("WebSocket send failed: " + ex.Message);
				frame.Recycle();
				await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Send failed").ConfigureAwait(false);
				return SendErrors.Disconnected;
			}
			finally
			{
				frame.Recycle();
			}

			return SendErrors.None;
		}

		public override Task Connect(MessageWriter _writer, int _timeout = 5000)
		{
			throw new InfinityException("Server connection cannot initiate Connect()");
		}

		protected override async Task DisconnectRemote(string _reason, MessageReader _reader)
		{
			MessageWriter frame = null;
			try
			{
				frame = WebSocketFrame.CreateFrame(ReadOnlySpan<byte>.Empty, 0, WebSocketOpcode.Close, true, false);
				stream.Write(frame.Buffer, 0, frame.Length);
			}
			catch { }
			finally
			{
				State = ConnectionState.NotConnected;
				frame?.Recycle();
				await InvokeDisconnected(_reason, _reader).ConfigureAwait(false);
				Dispose();
			}
		}

		protected override async Task DisconnectInternal(InfinityInternalErrors _error, string _reason)
		{
			OnInternalDisconnect?.Invoke(_error)?.ToReader()?.Recycle();
			State = ConnectionState.NotConnected;
			await InvokeDisconnected(_reason, null).ConfigureAwait(false);
			Dispose();
		}

		protected override bool SendDisconnect(MessageWriter _writer)
		{
			MessageWriter frame = null;
			try
			{
				frame = WebSocketFrame.CreateFrame(ReadOnlySpan<byte>.Empty, 0, WebSocketOpcode.Close, true, false);
				stream.Write(frame.Buffer, 0, frame.Length);
				frame.Recycle();
				State = ConnectionState.NotConnected;
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
			try
			{
				List<byte>? frag = null;
				WebSocketOpcode fragOpcode = WebSocketOpcode.Binary;
				int totalPayloadLen = 0;
				while (state == ConnectionState.Connected)
				{
					if (!WebSocketFrame.TryReadFrame(stream, out var opcode, out var fin, out var masked, out var payload))
					{
						await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Failed to read frame").ConfigureAwait(false);
						return;
					}

					if (closeReceived)
					{
						// After close received, ignore non-control frames
						if (opcode != WebSocketOpcode.Close && opcode != WebSocketOpcode.Ping && opcode != WebSocketOpcode.Pong)
						{
							continue;
						}
					}

					// Masking enforcement: client frames must be masked
					if (!masked)
					{
						var cw = MessageWriter.Get();
						cw.Write((byte)(1002 >> 8)); cw.Write((byte)(1002 & 0xFF));
						var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, false);
						await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
						frameClose.Recycle(); cw.Recycle();
						await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Unmasked client frame").ConfigureAwait(false);
						return;
					}

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
							var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, false);
							await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
							frameClose.Recycle(); cw.Recycle();
							await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Message too big").ConfigureAwait(false);
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
							var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, false);
							await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
							frameClose.Recycle(); cw.Recycle();
							await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Message too big").ConfigureAwait(false);
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
						var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, false);
						await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
						frameClose.Recycle(); cw.Recycle();
						await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Message too big").ConfigureAwait(false);
						return;
					}

					switch (opcode)
					{
						case WebSocketOpcode.Binary:
						case WebSocketOpcode.Continuation:
						{
							var reader = MessageReader.Get(payload, 0, payload.Length);
							await InvokeBeforeReceive(reader).ConfigureAwait(false);
							await InvokeDataReceived(reader).ConfigureAwait(false);
							break;
						}
						case WebSocketOpcode.Text:
						{
							// Validate UTF-8; close with 1007 if invalid
							try
							{
								var _ = new UTF8Encoding(false, true).GetString(payload);
							}
							catch (DecoderFallbackException)
							{
								var cw = MessageWriter.Get();
								cw.Write((byte)(1007 >> 8)); cw.Write((byte)(1007 & 0xFF));
								var frameClose = WebSocketFrame.CreateFrame(cw.Buffer.AsSpan(0, cw.Length), cw.Length, WebSocketOpcode.Close, true, false);
								await stream.WriteAsync(frameClose.Buffer, 0, frameClose.Length);
								frameClose.Recycle(); cw.Recycle();
								await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Invalid UTF-8").ConfigureAwait(false);
								return;
							}
							var reader = MessageReader.Get(payload, 0, payload.Length);
							await InvokeBeforeReceive(reader).ConfigureAwait(false);
							await InvokeDataReceived(reader).ConfigureAwait(false);
							break;
						}
						case WebSocketOpcode.Ping:
						{
							MessageWriter pong = WebSocketFrame.CreateFrame(payload.AsSpan(), payload.Length, WebSocketOpcode.Pong, true, false);
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
							// parse close code and reason if present
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
						// echo close once
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
							var frame = WebSocketFrame.CreateFrame(closeWriter.Buffer.AsSpan(0, closeWriter.Length), closeWriter.Length, WebSocketOpcode.Close, true, false);
							await stream.WriteAsync(frame.Buffer, 0, frame.Length);
							frame.Recycle(); closeWriter.Recycle();
							closeSent = true;
						}
						closeReceived = true;
								await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, string.IsNullOrEmpty(reason) ? "Remote closed" : reason).ConfigureAwait(false);
							return;
						}
						default:
						{
							// ignore unknown
							break;
						}
					}
				}
			}
			catch (IOException)
			{
				// Treat IO timeouts/abort as normal shutdown
				return;
			}
			catch (SocketException)
			{
				// Treat socket abort/reset as normal shutdown
				return;
			}
			catch (Exception ex)
			{
				if (shuttingDown || state != ConnectionState.Connected)
				{
					return;
				}
				logger?.WriteError("WebSocket receive loop failed: " + ex.Message);
					await DisconnectInternal(InfinityInternalErrors.ConnectionDisconnected, "Receive failed").ConfigureAwait(false);
			}
		}

		private void SendPing(object? _)
		{
			if (state != ConnectionState.Connected) return;
			MessageWriter frame = null;
			try
			{
				lastPingTicks = DateTime.UtcNow.Ticks;
				frame = WebSocketFrame.CreateFrame(ReadOnlySpan<byte>.Empty, 0, WebSocketOpcode.Ping, true, false);
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
			try { stream.Dispose(); } catch { }
			try { socket.Dispose(); } catch { }
			base.Dispose(_disposing);
		}
	}
}


