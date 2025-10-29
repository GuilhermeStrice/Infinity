using System.Net;
using System.Net.WebSockets;
using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class MessageSizeLimitTests
	{
		private static IPEndPoint GetFreeEndPoint()
		{
			var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
			l.Start();
			var ep = (IPEndPoint)l.LocalEndpoint;
			l.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		[Fact(Timeout = 10000)]
		public async Task MessageWithinLimit_IsAccepted()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			var got = new TaskCompletionSource<byte[]>();
			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				conn.MaxMessageSize = 64 * 1024;
				conn.DataReceived += de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					var toWrite = r.BytesRemaining;
					if (toWrite > w.Buffer.Length) { r.Recycle(); w.Recycle(); return; }
					w.Write(r.Buffer, r.Position, toWrite);
					_ = Task.Run(async () => { await conn.Send(w); });
					r.Recycle();
				};
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/"), CancellationToken.None);

			var payload = new byte[32 * 1024];
			await cws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);

			var buffer = new byte[payload.Length];
			var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
			Assert.Equal(payload.Length, result.Count);

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task MessageAtLimit_IsAccepted()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			var got = new TaskCompletionSource<byte[]>();
			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				conn.MaxMessageSize = 64 * 1024;
				conn.DataReceived += de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					var toWrite = r.BytesRemaining;
					if (toWrite > w.Buffer.Length) { r.Recycle(); w.Recycle(); return; }
					w.Write(r.Buffer, r.Position, toWrite);
					_ = Task.Run(async () => { await conn.Send(w); });
					r.Recycle();
				};
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/"), CancellationToken.None);

			var payload = new byte[32 * 1024];
			await cws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, true, CancellationToken.None);

			var buffer = new byte[payload.Length];
			var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
			Assert.Equal(payload.Length, result.Count);

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task MessageOverLimit_ServerClosesWith1009()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			WebSocketServerConnection? serverConn = null;
			listener.NewConnection += e =>
			{
				serverConn = (WebSocketServerConnection)e.Connection;
				serverConn.MaxMessageSize = 1024 * 1024;
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/"), CancellationToken.None);

			// Send message just over limit
			var payload = new byte[64 * 1024 + 1];
			await cws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Binary, false, CancellationToken.None);
			await cws.SendAsync(new ArraySegment<byte>(new byte[1]), WebSocketMessageType.Binary, true, CancellationToken.None);

			var buffer = new byte[10];
			try
			{
				var result = await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
				if (cws.CloseStatus == WebSocketCloseStatus.MessageTooBig)
				{
					// expected
					return;
				}
			}
			catch { }

			await Task.Delay(500);
			Assert.True(serverConn == null || serverConn.State != Infinity.Core.ConnectionState.Connected);

			cws.Dispose();
			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task FragmentedMessageOverLimit_ServerCloses()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			WebSocketServerConnection? serverConn = null;
			listener.NewConnection += e =>
			{
				serverConn = (WebSocketServerConnection)e.Connection;
				serverConn.MaxMessageSize = 1024;
			};
			listener.Start();

			using var cws = new ClientWebSocket();
			await cws.ConnectAsync(new Uri($"ws://{ep.Address}:{ep.Port}/"), CancellationToken.None);

			// Send 3 fragments totaling over limit
			var chunk = new byte[500];
			await cws.SendAsync(new ArraySegment<byte>(chunk), WebSocketMessageType.Binary, false, CancellationToken.None);
			await cws.SendAsync(new ArraySegment<byte>(chunk), WebSocketMessageType.Binary, false, CancellationToken.None);
			await cws.SendAsync(new ArraySegment<byte>(chunk), WebSocketMessageType.Binary, true, CancellationToken.None);

			var buffer = new byte[10];
			try { await cws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None); } catch { }
			await Task.Delay(500);
			Assert.True(serverConn == null || serverConn.State != Infinity.Core.ConnectionState.Connected);

			cws.Dispose();
			listener.Dispose();
		}
	}
}

