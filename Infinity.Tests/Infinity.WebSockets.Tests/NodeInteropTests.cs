using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class NodeInteropTests
	{
		ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

		private static IPEndPoint GetFreeEndPoint()
		{
			var listener = new TcpListener(IPAddress.Loopback, 0);
			listener.Start();
			var ep = (IPEndPoint)listener.LocalEndpoint;
			listener.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		private static bool HasNode()
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo
				{
					FileName = "node",
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

		private static bool HasNodeWs()
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo
				{
					FileName = "node",
					Arguments = "-e \"require('ws'); console.log('ok')\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				});
				p.WaitForExit(4000);
				return p.ExitCode == 0;
			}
			catch { return false; }
		}

		private static string WriteTemp(string content, string ext)
		{
			var path = Path.Combine(Path.GetTempPath(), $"node_ws_{Guid.NewGuid():N}.{ext}");
			File.WriteAllText(path, content);
			return path;
		}

		[Fact(Timeout = 20000)]
		public async Task NodeServer_EchoBinary()
		{
			if (!HasNode())
			{
				Console.WriteLine("Skipping NodeServer_EchoBinary: node not found");
				return;
			}
			if (!HasNodeWs())
			{
				Console.WriteLine("Skipping NodeServer_EchoBinary: ws package not available");
				return;
			}

			var ep = GetFreeEndPoint();
			var serverJs = @"const WebSocket = require('ws');
const port = Number(process.env.PORT||3000);
const wss = new WebSocket.Server({ port });
wss.on('connection', ws => {
  ws.on('message', message => ws.send(message));
});
setTimeout(()=>{}, 1e9);
";

			var path = WriteTemp(serverJs, "js");
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "node",
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["PORT"] = ep.Port.ToString() }
			});

			// Wait for port ready
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < 5000)
			{
				try { using var t = new TcpClient(); await t.ConnectAsync(ep.Address, ep.Port); break; } catch { await Task.Delay(50); }
			}

			var logger = new TestLogger("WS");
			var client = new WebSocketClientConnection(logger);
			var connectWriter = new MessageWriter(allocator);
			connectWriter.Write($"ws://{ep.Address}:{ep.Port}/");
			await client.Connect(connectWriter);

			var tcs = new TaskCompletionSource<byte[]>();
			client.DataReceived += async de => { var b = de.Message.ReadBytes(de.Message.BytesRemaining); tcs.TrySetResult(b); };

			var payload = Enumerable.Range(0, 4096).Select(i => (byte)(i % 256)).ToArray();
			var msg = new MessageWriter(allocator);
			msg.Write(payload, 0, payload.Length);
			await client.Send(msg);

			var echoed = await tcs.Task;
			Assert.Equal(payload, echoed);

			await client.Disconnect("done", new MessageWriter(allocator));
			try { proc.Kill(true); } catch { }
		}

		[Fact(Timeout = 20000)]
		public async Task NodeServer_InvalidUtf8Text_ClientCloses()
		{
			if (!HasNode()) { Console.WriteLine("Skipping NodeServer_InvalidUtf8Text_ClientCloses: node not found"); return; }
			if (!HasNodeWs()) { Console.WriteLine("Skipping NodeServer_InvalidUtf8Text_ClientCloses: ws package not available"); return; }

			var ep = GetFreeEndPoint();
			var serverJs = @"const WebSocket = require('ws');
const port = Number(process.env.PORT||3000);
const wss = new WebSocket.Server({ port });
wss.on('connection', ws => {
  const buf = Buffer.from([0xff, 0xfe, 0xfd]);
  ws.send(buf, { binary: false }); // send as text with invalid UTF-8
});
setTimeout(()=>{}, 1e9);
";
			var path = WriteTemp(serverJs, "js");
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "node",
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["PORT"] = ep.Port.ToString() }
			});

			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < 5000) { try { using var t = new TcpClient(); await t.ConnectAsync(ep.Address, ep.Port); break; } catch { await Task.Delay(50); } }

			var logger = new TestLogger("WS");
			var client = new WebSocketClientConnection(logger);
			var connectWriter = new MessageWriter(allocator);
			connectWriter.Write($"ws://{ep.Address}:{ep.Port}/");
			await client.Connect(connectWriter);

			// Wait briefly for disconnect due to invalid UTF-8
			await Task.Delay(500);
			Assert.True(client.State != Infinity.Core.ConnectionState.Connected);

			try { proc.Kill(true); } catch { }
		}

		[Fact(Timeout = 20000)]
		public async Task NodeClient_InvalidUtf8Text_ServerCloses()
		{
			if (!HasNode()) { Console.WriteLine("Skipping NodeClient_InvalidUtf8Text_ServerCloses: node not found"); return; }
			if (!HasNodeWs()) { Console.WriteLine("Skipping NodeClient_InvalidUtf8Text_ServerCloses: ws package not available"); return; }

			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			WebSocketServerConnection? serverConn = null;
			listener.NewConnection += e => { serverConn = (WebSocketServerConnection)e.Connection; };
			listener.Start();

			var clientJs = @"const WebSocket = require('ws');
const url = process.env.URL;
const ws = new WebSocket(url);
ws.on('open', () => {
  const buf = Buffer.from([0xff, 0xfe, 0xfd]);
  ws.send(buf, { binary: false });
});
setTimeout(()=>{}, 1e9);
";
			var path = WriteTemp(clientJs, "js");
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "node",
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["URL"] = $"ws://{ep.Address}:{ep.Port}/" }
			});

			var wait = Stopwatch.StartNew();
			while (serverConn == null && wait.ElapsedMilliseconds < 5000) await Task.Delay(50);
			Assert.NotNull(serverConn);

			// Give time for server to process invalid UTF-8 and close
			await Task.Delay(500);
			Assert.True(serverConn.State != Infinity.Core.ConnectionState.Connected);

			try { proc.Kill(true); } catch { }
			listener.Dispose();
		}

		[Fact(Timeout = 20000)]
		public async Task NodeClient_EchoesServerMessage()
		{
			if (!HasNode())
			{
				Console.WriteLine("Skipping NodeClient_EchoesServerMessage: node not found");
				return;
			}
			if (!HasNodeWs())
			{
				Console.WriteLine("Skipping NodeClient_EchoesServerMessage: ws package not available");
				return;
			}

			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);

			var echoedTcs = new TaskCompletionSource<byte[]>();
			WebSocketServerConnection? serverConn = null;
			listener.NewConnection += e =>
			{
				serverConn = (WebSocketServerConnection)e.Connection;
				serverConn.DataReceived += async de => { var b = de.Message.ReadBytes(de.Message.BytesRemaining); echoedTcs.TrySetResult(b); };
			};
			listener.Start();

			var clientJs = @"const WebSocket = require('ws');
const url = process.env.URL;
const ws = new WebSocket(url);
ws.on('message', data => ws.send(data));
setTimeout(()=>{}, 1e9);
";
			var path = WriteTemp(clientJs, "js");
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = "node",
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

			var payload = Encoding.UTF8.GetBytes("node-interop");
			var msg = new MessageWriter(allocator);
			msg.Write(payload, 0, payload.Length);
			await serverConn!.Send(msg);

			var echoed = await echoedTcs.Task;
			Assert.Equal(payload, echoed);

			try { proc.Kill(true); } catch { }
			listener.Dispose();
		}
	}
}


