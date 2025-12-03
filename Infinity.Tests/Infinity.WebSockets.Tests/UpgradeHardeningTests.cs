using System.Net;
using System.Net.Sockets;
using System.Text;
using Infinity.Tests.Core;
using Infinity.WebSockets;

namespace Infinity.Websockets.Tests
{
	public class UpgradeHardeningTests
	{
		private static IPEndPoint GetFreeEndPoint()
		{
			var l = new TcpListener(IPAddress.Loopback, 0);
			l.Start();
			var ep = (IPEndPoint)l.LocalEndpoint;
			l.Stop();
			return new IPEndPoint(IPAddress.Loopback, ep.Port);
		}

		private static async Task<string> SendRawRequest(IPEndPoint ep, string request)
		{
			using var tcp = new TcpClient();
			await tcp.ConnectAsync(ep.Address, ep.Port);
			var stream = tcp.GetStream();
			var bytes = Encoding.ASCII.GetBytes(request);
			await stream.WriteAsync(bytes, 0, bytes.Length);
			await stream.FlushAsync();

			// read response headers
			var sb = new StringBuilder();
			byte[] buf = new byte[1024];
			int matched = 0;
			while (true)
			{
				int n = await stream.ReadAsync(buf, 0, buf.Length);
				if (n <= 0) break;
				sb.Append(Encoding.ASCII.GetString(buf, 0, n));
				for (int i = 0; i < n; i++)
				{
					char c = (char)buf[i];
					if ((matched == 0 || matched == 2) && c == '\r') matched++;
					else if ((matched == 1 || matched == 3) && c == '\n') matched++;
					else matched = 0;
					if (matched == 4) return sb.ToString();
				}
			}

			return sb.ToString();
		}

		[Fact(Timeout = 10000)]
		public async Task MissingUpgradeHeader_IsRejected()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			bool newConn = false;
			listener.NewConnection += _ => newConn = true;
			listener.Start();

			string req =
				$"GET / HTTP/1.1\r\n" +
				$"Host: {ep.Address}:{ep.Port}\r\n" +
				"Connection: Upgrade\r\n" +
				"Sec-WebSocket-Key: dGVzdHRlc3R0ZXN0dGVzdA==\r\n" +
				"Sec-WebSocket-Version: 13\r\n\r\n";

			string resp = await SendRawRequest(ep, req);
			Assert.StartsWith("HTTP/1.1 400", resp);
			Assert.False(newConn);

			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task MissingConnectionHeader_IsRejected()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			bool newConn = false;
			listener.NewConnection += _ => newConn = true;
			listener.Start();

			string req =
				$"GET / HTTP/1.1\r\n" +
				$"Host: {ep.Address}:{ep.Port}\r\n" +
				"Upgrade: websocket\r\n" +
				"Sec-WebSocket-Key: dGVzdHRlc3R0ZXN0dGVzdA==\r\n" +
				"Sec-WebSocket-Version: 13\r\n\r\n";

			string resp = await SendRawRequest(ep, req);
			Assert.StartsWith("HTTP/1.1 400", resp);
			Assert.False(newConn);

			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task WrongVersion_IsRejected()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			bool newConn = false;
			listener.NewConnection += _ => newConn = true;
			listener.Start();

			string req =
				$"GET / HTTP/1.1\r\n" +
				$"Host: {ep.Address}:{ep.Port}\r\n" +
				"Upgrade: websocket\r\n" +
				"Connection: Upgrade\r\n" +
				"Sec-WebSocket-Key: dGVzdHRlc3R0ZXN0dGVzdA==\r\n" +
				"Sec-WebSocket-Version: 12\r\n\r\n";

			string resp = await SendRawRequest(ep, req);
			Assert.StartsWith("HTTP/1.1 400", resp);
			Assert.False(newConn);

			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task BadKey_IsRejected()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger);
			bool newConn = false;
			listener.NewConnection += _ => newConn = true;
			listener.Start();

			// Not base64
			string req =
				$"GET / HTTP/1.1\r\n" +
				$"Host: {ep.Address}:{ep.Port}\r\n" +
				"Upgrade: websocket\r\n" +
				"Connection: Upgrade\r\n" +
				"Sec-WebSocket-Key: @@@@@\r\n" +
				"Sec-WebSocket-Version: 13\r\n\r\n";

			string resp = await SendRawRequest(ep, req);
			Assert.StartsWith("HTTP/1.1 400", resp);
			Assert.False(newConn);

			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task Subprotocol_IsNegotiated_WhenOffered()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger)
			{
				ProtocolSelector = offered => offered.Contains("chat") ? "chat" : null
			};
			listener.Start();

			string req =
				$"GET / HTTP/1.1\r\n" +
				$"Host: {ep.Address}:{ep.Port}\r\n" +
				"Upgrade: websocket\r\n" +
				"Connection: Upgrade\r\n" +
				"Sec-WebSocket-Key: dGVzdHRlc3R0ZXN0dGVzdA==\r\n" +
				"Sec-WebSocket-Version: 13\r\n" +
				"Sec-WebSocket-Protocol: chat, superchat\r\n\r\n";

			string resp = await SendRawRequest(ep, req);
			Assert.StartsWith("HTTP/1.1 101", resp);
			Assert.Contains("Sec-WebSocket-Protocol: chat", resp);

			listener.Dispose();
		}

		[Fact(Timeout = 10000)]
		public async Task ClientRequestsProtocol_ServerAccepts_ClientObserves()
		{
			var ep = GetFreeEndPoint();
			var logger = new TestLogger("WS");
			var listener = new WebSocketConnectionListener(ep, logger)
			{
				ProtocolSelector = offered => offered.Contains("echo") ? "echo" : null
			};
			listener.Start();

			var client = new WebSocketClientConnection(logger)
			{
				RequestedProtocol = "echo"
			};
			var w = Infinity.Core.MessageWriter.Get();
			w.Write($"ws://{ep.Address}:{ep.Port}/");
			await client.Connect(w);
			w.Recycle();

			Assert.Equal("echo", client.AcceptedProtocol);

			await client.Disconnect("done", Infinity.Core.MessageWriter.Get());
			listener.Dispose();
		}
	}
}


