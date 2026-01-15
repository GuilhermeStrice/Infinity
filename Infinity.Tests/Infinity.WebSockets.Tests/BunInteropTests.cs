using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class BunInteropTests
	{
		ChunkAllocator allocator = new ChunkAllocator(1024);

		private static IPEndPoint GetFreeEndPoint()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var ep = (IPEndPoint)listener.LocalEndpoint;
			listener.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		private static bool HasBun()
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo
				{
					FileName = "bun",
					Arguments = "-v",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				});
				p.WaitForExit(2000);
				return p.ExitCode == 0;
			}
			catch { return false; }
		}

		private static string WriteTempScript(string contents)
		{
			var path = Path.Combine(Path.GetTempPath(), $"bun_ws_{Guid.NewGuid():N}.ts");
			File.WriteAllText(path, contents);
			return path;
		}

		private static async Task<bool> WaitPortAsync(IPEndPoint ep, int timeoutMs = 3000)
		{
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				try
				{
					using var tcp = new TcpClient();
					await tcp.ConnectAsync(ep.Address, ep.Port);
					return true;
				}
				catch { await Task.Delay(50); }
			}
			return false;
		}

		[Fact(Timeout = 20000)]
		public async Task BunServer_EchoBinary()
		{
			if (!HasBun())
			{
				Console.WriteLine("Skipping BunServer_EchoBinary: bun not found");
				return;
			}

			var ep = GetFreeEndPoint();
			var serverScript = @"const port = Number(process.env.PORT||3000);
			Bun.serve({
			  port,
			  fetch(req, server) {
			    if (server.upgrade(req)) return;
			    return new Response('upgrade required', { status: 426 });
			  },
			  websocket: {
			    message(ws, message) {
			      ws.send(message);
			    }
			  }
			});
			setTimeout(()=>{}, 1e9);
			";

			var path = WriteTempScript(serverScript);
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "bun",
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["PORT"] = ep.Port.ToString() }
			});

			await WaitPortAsync(ep);

			var logger = new TestLogger("WS");
			var client = new WebSocketClientConnection(logger);
			var connectWriter = new MessageWriter(allocator);
			connectWriter.Write($"ws://{ep.Address}:{ep.Port}/");
			await client.Connect(connectWriter);

			var tcs = new TaskCompletionSource<byte[]>();
			client.DataReceived += async de =>
			{
				var bytes = de.Message.ReadBytes(de.Message.BytesRemaining);
				tcs.TrySetResult(bytes);
			};

			var payload = Enumerable.Range(0, 5000).Select(i => (byte)(i % 256)).ToArray();
			var msg = new MessageWriter(allocator);
			msg.Write(payload, 0, payload.Length);
			await client.Send(msg);

			var echoed = await tcs.Task;
			Assert.Equal(payload, echoed);

			await client.Disconnect("done", new MessageWriter(allocator));
			try { proc.Kill(true); } catch { }
		}

		[Fact(Timeout = 20000)]
		public async Task BunClient_EchoesServerMessage()
		{
			if (!HasBun())
			{
				Console.WriteLine("Skipping BunClient_EchoesServerMessage: bun not found");
				return;
			}

			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			var echoedTcs = new TaskCompletionSource<byte[]>();
			WebSocketServerConnection serverConn = null;
			listener.NewConnection += e =>
			{
				serverConn = (WebSocketServerConnection)e.Connection;
				serverConn.DataReceived += async de =>
				{
					var bytes = de.Message.ReadBytes(de.Message.BytesRemaining);
					echoedTcs.TrySetResult(bytes);
				};
			};
			listener.Start();

			var clientScript = @"const url = process.env.URL;
			const ws = new WebSocket(url);
			ws.binaryType = 'arraybuffer';
			ws.onmessage = (ev)=>{ ws.send(ev.data); };
			setTimeout(()=>{}, 1e9);
			";
			var path = WriteTempScript(clientScript);
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "bun",
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["URL"] = $"ws://{ep.Address}:{ep.Port}/" }
			});

			// wait for connection
			var sw = Stopwatch.StartNew();
			while (serverConn == null && sw.ElapsedMilliseconds < 5000) await Task.Delay(50);
			Assert.NotNull(serverConn);

			var payload = Encoding.UTF8.GetBytes("interop-bun");
			var msg = new MessageWriter(allocator);
			msg.Write(payload, 0, payload.Length);
			await serverConn.Send(msg);

			var echoed = await echoedTcs.Task;
			Assert.Equal(payload, echoed);

			try { proc.Kill(true); } catch { }
			listener.Dispose();
		}
	}
}


