using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class PythonInteropTests
	{
		ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

		private static IPEndPoint GetFreeEndPoint()
		{
			var l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			var ep = (IPEndPoint)l.LocalEndpoint;
			l.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		private static string? FindPython()
		{
			string[] candidates = new[] { "python3", "python" };
			foreach (var exe in candidates)
			{
				try
				{
					var p = Process.Start(new ProcessStartInfo { FileName = exe, Arguments = "--version", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
					p.WaitForExit(2000);
					if (p.ExitCode == 0) return exe;
				}
				catch { }
			}
			return null;
		}

		private static bool HasPythonWebsockets(string py)
		{
			try
			{
				var p = Process.Start(new ProcessStartInfo
				{
					FileName = py,
					Arguments = "-c \"import websockets, asyncio; print('ok')\"",
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
			var path = Path.Combine(Path.GetTempPath(), $"py_ws_{Guid.NewGuid():N}.{ext}");
			File.WriteAllText(path, content);
			return path;
		}

		[Fact(Timeout = 25000)]
		public async Task PythonServer_EchoBinary()
		{
			var py = FindPython();
			if (py == null)
			{
				Console.WriteLine("Skipping PythonServer_EchoBinary: python not found (python3/python)");
				return;
			}
			if (!HasPythonWebsockets(py))
			{
				Console.WriteLine("Skipping PythonServer_EchoBinary: python websockets package not installed");
				return;
			}

			var ep = GetFreeEndPoint();
			var serverCode = @"import asyncio, websockets, os
port = int(os.environ['PORT'])
async def echo(ws):
    async for msg in ws:
        await ws.send(msg)
async def main():
    async with websockets.serve(echo, '127.0.0.1', port, max_size=None):
        await asyncio.Future()
asyncio.run(main())
";

			var path = WriteTemp(serverCode, "py");
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = py,
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["PORT"] = ep.Port.ToString() }
			});

			// wait for port
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < 5000)
			{
				try { using var t = new TcpClient(); await t.ConnectAsync(ep.Address, ep.Port); break; } catch { await Task.Delay(50); }
			}

			var logger = new TestLogger("WS");
			var client = new WebSocketClientConnection(logger);
			var connect = new MessageWriter(allocator);
			connect.Write($"ws://{ep.Address}:{ep.Port}/");
			await client.Connect(connect);

			var tcs = new TaskCompletionSource<byte[]>();
			client.DataReceived += async de => { var b = de.Message.ReadBytes(de.Message.BytesRemaining); tcs.TrySetResult(b); };

			var payload = Enumerable.Range(0, 2048).Select(i => (byte)(i % 256)).ToArray();
			var msg = new MessageWriter(allocator);
			msg.Write(payload, 0, payload.Length);
			await client.Send(msg);

			var echoed = await tcs.Task;
			Assert.Equal(payload, echoed);

			await client.Disconnect("done", new MessageWriter(allocator));
			try { proc.Kill(true); } catch { }
		}

		[Fact(Timeout = 25000)]
		public async Task PythonClient_EchoesServerMessage()
		{
			var py = FindPython();
			if (py == null)
			{
				Console.WriteLine("Skipping PythonClient_EchoesServerMessage: python not found (python3/python)");
				return;
			}
			if (!HasPythonWebsockets(py))
			{
				Console.WriteLine("Skipping PythonClient_EchoesServerMessage: python websockets package not installed");
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

			var clientCode = @"import asyncio, websockets, os
url = os.environ['URL']
async def run():
    async with websockets.connect(url, max_size=None) as ws:
        async for msg in ws:
            await ws.send(msg)
asyncio.run(run())
";
			var path = WriteTemp(clientCode, "py");
			var proc = Process.Start(new ProcessStartInfo
			{
				FileName = py,
				Arguments = path,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				Environment = { ["URL"] = $"ws://{ep.Address}:{ep.Port}/" }
			});

			var waitSw = Stopwatch.StartNew();
			while (serverConn == null && waitSw.ElapsedMilliseconds < 5000) await Task.Delay(50);
			Assert.NotNull(serverConn);

			var payload = Encoding.UTF8.GetBytes("python-interop");
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


