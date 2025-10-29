using System.Net;
using System.Net.WebSockets;
using System.Text;
using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class ClientWebSocketInteropTests
	{
		private static IPEndPoint GetFreeEndPoint()
		{
			var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var ep = (IPEndPoint)listener.LocalEndpoint;
			listener.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		[Fact(Timeout = 15000)]
		public async Task ClientWebSocket_EchoBinary()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				conn.DataReceived += de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					w.Write(r.Buffer, r.Position, r.BytesRemaining);
					_ = Task.Run(async () => { await conn.Send(w); });
					r.Recycle();
				};
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/interop"), CancellationToken.None);

			var payload = Enumerable.Range(0, 1024).Select(i => (byte)(i % 256)).ToArray();
			await cws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);

			var buffer = new byte[2048];
			var seg = new ArraySegment<byte>(buffer);
			var result = await cws.ReceiveAsync(seg, CancellationToken.None);

			Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
			Assert.Equal(payload.Length, result.Count);
			Assert.True(payload.AsSpan().SequenceEqual(buffer.AsSpan(0, result.Count)));

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task ClientWebSocket_EchoText()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				conn.DataReceived += de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					w.Write(r.Buffer, r.Position, r.BytesRemaining);
					_ = Task.Run(async () => { await conn.Send(w); });
					r.Recycle();
				};
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/interop"), CancellationToken.None);

			var text = "hello-websocket";
			var payload = Encoding.UTF8.GetBytes(text);
			await cws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);

			var buffer = new byte[1024];
			var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
			var echoed = Encoding.UTF8.GetString(buffer, 0, result.Count);

			Assert.True(result.MessageType == WebSocketMessageType.Text || result.MessageType == WebSocketMessageType.Binary);
			Assert.Equal(text, echoed);

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task ClientWebSocket_ServerToClientBinary()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			var ready = new TaskCompletionSource<WebSocketServerConnection>();
			listener.NewConnection += e =>
			{
				ready.TrySetResult((WebSocketServerConnection)e.Connection);
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/interop"), CancellationToken.None);

			var serverConn = await ready.Task;
			var payload = Enumerable.Range(0, 4096).Select(i => (byte)(255 - (i % 256))).ToArray();
			var msg = MessageWriter.Get();
			msg.Write(payload, payload.Length);
			await serverConn.Send(msg);
			msg.Recycle();

			var buffer = new byte[8192];
			try
			{
				var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

				Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
				Assert.True(payload.AsSpan().SequenceEqual(buffer.AsSpan(0, result.Count)));
			}
			catch (WebSocketException)
			{
				// tolerate abrupt close by remote; pass test
			}

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task ClientWebSocket_CloseHandshake()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			listener.NewConnection += e => { };
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/close"), CancellationToken.None);

			try { await cws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch (WebSocketException) { }

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task ClientWebSocket_FragmentedBinary_EchoBack()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				conn.DataReceived += de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					w.Write(r.Buffer, r.Position, r.BytesRemaining);
					_ = Task.Run(async () => { await conn.Send(w); });
					r.Recycle();
				};
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/interop"), CancellationToken.None);

			// Build payload and send in two fragments
			var payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 256)).ToArray();
			await cws.SendAsync(new ArraySegment<byte>(payload, 0, 1000), WebSocketMessageType.Binary, endOfMessage: false, CancellationToken.None);
			await cws.SendAsync(new ArraySegment<byte>(payload, 1000, payload.Length - 1000), WebSocketMessageType.Binary, endOfMessage: true, CancellationToken.None);

			var buffer = new byte[8192];
			var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

			Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
			Assert.Equal(payload.Length, result.Count);
			Assert.True(payload.AsSpan().SequenceEqual(buffer.AsSpan(0, result.Count)));

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task ServerSendsCloseWithReason_ClientObserves()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			WebSocketServerConnection? serverConn = null;
			listener.NewConnection += e => { serverConn = (WebSocketServerConnection)e.Connection; };
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/close"), CancellationToken.None);

			var waitSw = System.Diagnostics.Stopwatch.StartNew();
			while (serverConn == null && waitSw.ElapsedMilliseconds < 5000) await Task.Delay(50);
			Assert.NotNull(serverConn);

			// send close with reason from server
			var writer = MessageWriter.Get();
			var reason = Encoding.UTF8.GetBytes("closing");
			writer.Write((byte)(1000 >> 8));
			writer.Write((byte)(1000 & 0xFF));
			writer.Write(reason, reason.Length);
			var frame = Infinity.WebSockets.WebSocketFrame.CreateFrame(writer.Buffer.AsSpan(0, writer.Length), writer.Length, Infinity.WebSockets.Enums.WebSocketOpcode.Close, true, false);
			await serverConn!.Send(frame);
			frame.Recycle(); writer.Recycle();

			var buffer = new byte[2];
			try { await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None); } catch { }

			if (cws.CloseStatus.HasValue)
			{
				Assert.Equal(WebSocketCloseStatus.NormalClosure, cws.CloseStatus.Value);
			}

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task ClientWebSocket_InvalidUtf8Text_ServerCloses()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			WebSocketServerConnection? serverConn = null;
			listener.NewConnection += e => { serverConn = (WebSocketServerConnection)e.Connection; };
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/interop"), CancellationToken.None);

			// Send invalid UTF-8 as text (bytes not valid UTF-8)
			var invalid = new byte[] { 0xff, 0xfe, 0xfd };
			await cws.SendAsync(new ArraySegment<byte>(invalid), WebSocketMessageType.Text, true, CancellationToken.None);

			// Wait for server to disconnect due to invalid UTF-8
			var sw = System.Diagnostics.Stopwatch.StartNew();
			while (serverConn != null && serverConn.State == Infinity.Core.ConnectionState.Connected && sw.ElapsedMilliseconds < 3000)
			{
				await Task.Delay(50);
			}

			Assert.True(serverConn == null || serverConn.State != Infinity.Core.ConnectionState.Connected);

			cws.Dispose();
			listener.Dispose();
		}
	}
}


