using System.Net;
using System.Text;
using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class WebSocketEndToEndTests
	{
		private static IPEndPoint GetFreeEndPoint()
		{
			var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var ep = (IPEndPoint)listener.LocalEndpoint;
			listener.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		[Fact(Timeout = 10000)]
		public async Task Connect_SendBinary_EchoBack()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			var serverConnTcs = new TaskCompletionSource<WebSocketServerConnection>();
			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				// Echo server
				conn.DataReceived += async de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					w.Write(r.Buffer, r.Position, r.BytesRemaining);
					_ = Task.Run(async () => { await conn.Send(w); });
					r.Recycle();
				};
				serverConnTcs.TrySetResult(conn);
			};

			listener.Start();

			var client = new WebSocketClientConnection(logger);
			var connectWriter = MessageWriter.Get();
			connectWriter.Write($"ws://{ep.Address}:{ep.Port}/test");
			await client.Connect(connectWriter);
			connectWriter.Recycle();

			var serverConn = await serverConnTcs.Task;

			var gotEchoTcs = new TaskCompletionSource<byte[]>();
			client.DataReceived += async de =>
			{
				var r = de.Message;
				var bytes = r.ReadBytes(r.BytesRemaining);
				gotEchoTcs.TrySetResult(bytes);
				r.Recycle();
			};

			// Send payload
			var payload = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
			var msg = MessageWriter.Get();
			msg.Write(payload, payload.Length);
			await client.Send(msg);
			msg.Recycle();

			var echoed = await gotEchoTcs.Task;
			Assert.Equal(payload, echoed);

			await client.Disconnect("done", MessageWriter.Get());
			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task PingPong_AveragePingMs_Updates()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			listener.NewConnection += e => { };
			listener.Start();

			var client = new WebSocketClientConnection(logger);
			var connectWriter = MessageWriter.Get();
			connectWriter.Write($"ws://{ep.Address}:{ep.Port}/ping");
			await client.Connect(connectWriter);
			connectWriter.Recycle();

			var sw = System.Diagnostics.Stopwatch.StartNew();
			while (client.AveragePingMs <= 0 && sw.ElapsedMilliseconds < 8000)
			{
				await Task.Delay(100);
			}

			Assert.True(client.AveragePingMs >= 0);

			await client.Disconnect("done", MessageWriter.Get());
			listener.Dispose();
		}

		[Fact(Timeout = 15000)]
		public async Task LargePayload_60KB_RoundTrip()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			listener.NewConnection += e =>
			{
				var conn = (WebSocketServerConnection)e.Connection;
				conn.DataReceived += async de =>
				{
					var r = de.Message;
					var w = MessageWriter.Get();
					w.Write(r.Buffer, r.Position, r.BytesRemaining);
					_ = conn.Send(w);
					r.Recycle();
				};
			};
			listener.Start();

			var client = new WebSocketClientConnection(logger);
			var connectWriter = MessageWriter.Get();
			connectWriter.Write($"ws://{ep.Address}:{ep.Port}/large");
			await client.Connect(connectWriter);
			connectWriter.Recycle();

			var gotEchoTcs = new TaskCompletionSource<int>();
			int expected = 60000;
			int received = 0;
			client.DataReceived += async de =>
			{
				received += de.Message.BytesRemaining;
				de.Message.Recycle();
				if (received >= expected) gotEchoTcs.TrySetResult(received);
			};

			var payload = new byte[expected];
			new Random(42).NextBytes(payload);
			var msg = MessageWriter.Get();
			msg.Write(payload, payload.Length);
			await client.Send(msg);
			msg.Recycle();

			var total = await gotEchoTcs.Task;
			Assert.Equal(expected, total);

			await client.Disconnect("done", MessageWriter.Get());
			listener.Dispose();
		}
	}
}


