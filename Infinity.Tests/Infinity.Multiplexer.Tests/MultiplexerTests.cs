using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.Udp;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using Xunit;
using System.Threading.Tasks;
using System;
using Infinity.WebSockets;

namespace Infinity.Multiplexer.Tests
{
    public class MultiplexerTests
    {
        ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

        //[Fact]
        public async Task MultiplexerTest_Csharp()
        {
            var udp_data = new byte[] { 1, 2, 3 };
            var ws_data = new byte[] { 4, 5, 6 };

            int port = Util.GetFreePort();

            var logger = new TestLogger("MultiplexerTest");
            var listener = new InfinityConnectionListener(new IPEndPoint(IPAddress.Any, port), logger);

            var serverUdpReceive = new TaskCompletionSource<bool>();
            var serverWsReceive = new TaskCompletionSource<bool>();
            var clientUdpReceive = new TaskCompletionSource<bool>();
            var clientWsReceive = new TaskCompletionSource<bool>();

            NetworkConnection server_udp_connection = null;
            NetworkConnection server_ws_connection = null;

            // ------------------------
            // Server connection handler
            // ------------------------
            listener.NewConnection += (e) =>
            {
                logger.WriteInfo("New connection");

                if (e.Connection is UdpServerConnection)
                {
                    server_udp_connection = e.Connection;

                    e.Connection.DataReceived += async (e2) =>
                    {
                        serverUdpReceive.TrySetResult(true);

                        // Echo UDP (keeps internal headers intact)
                        var writer = new MessageWriter(allocator);
                        writer.Write(e2.Message.Buffer, 0, e2.Message.Length);
                        await e.Connection.Send(writer);
                    };
                }
                else
                {
                    server_ws_connection = e.Connection;

                    e.Connection.DataReceived += async (e2) =>
                    {
                        serverWsReceive.TrySetResult(true);

                        // Echo WS payload (no headers)
                        var writer = new MessageWriter(allocator);
                        writer.Write(e2.Message.Buffer, 0, e2.Message.Length);
                        await e2.Connection.Send(writer);
                    };
                }
            };

            listener.Start();

            // ------------------------
            // UDP Client
            // ------------------------
            var udp_client = new UdpClientConnection(logger, new IPEndPoint(IPAddress.Loopback, port));

            udp_client.DataReceived += async (e) =>
            {
                clientUdpReceive.TrySetResult(true);

                // Skip first 2 bytes (example of UDP header)
                for (int i = 0; i < udp_data.Length; i++)
                {
                    Assert.Equal(udp_data[i], e.Message.Buffer[i + 2]);
                }
            };

            var handshake = UdpMessageFactory.BuildHandshakeMessage(udp_client);
            await udp_client.Connect(handshake);

            // ------------------------
            // WebSocket Client using System.Net.WebSockets
            // ------------------------
            using var ws_client = new ClientWebSocket();
            var ws_uri = new Uri($"ws://127.0.0.1:{port}");
            await ws_client.ConnectAsync(ws_uri, CancellationToken.None);

            // Send WS data
            await ws_client.SendAsync(new ArraySegment<byte>(ws_data), WebSocketMessageType.Binary, true, CancellationToken.None);

            // Receive WS echo
            var buffer = new byte[1024];
            var result = await ws_client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            clientWsReceive.TrySetResult(true);

            // Assert received WS payload
            for (int i = 0; i < ws_data.Length; i++)
            {
                Assert.Equal(ws_data[i], buffer[i]);
            }

            // ------------------------
            // Wait for server events
            // ------------------------
            await Task.WhenAny(serverWsReceive.Task, Task.Delay(2000));
            await Task.WhenAny(clientWsReceive.Task, Task.Delay(2000));

            // ------------------------
            // Cleanup
            // ------------------------
            await udp_client.Disconnect("test", new MessageWriter(allocator));
            if (ws_client.State == WebSocketState.Open)
            {
                await ws_client.CloseAsync(WebSocketCloseStatus.NormalClosure, "test", CancellationToken.None);
            }

            listener.Dispose();
        }

        [Fact]
        public async Task MultiplexerTest()
        {
            var udp_data = new byte[] { 1, 2, 3 };
            var ws_data = new byte[] { 4, 5, 6 };

            int port = Util.GetFreePort();

            var logger = new TestLogger("MultiplexerTest");
            var listener = new InfinityConnectionListener(new IPEndPoint(IPAddress.Any, port), logger);

            var serverUdpReceive = new TaskCompletionSource<bool>();
            var serverWsReceive = new TaskCompletionSource<bool>();
            var clientUdpReceive = new TaskCompletionSource<bool>();
            var clientWsReceive = new TaskCompletionSource<bool>();

            NetworkConnection server_udp_connection = null;
            NetworkConnection server_ws_connection = null;

            // ------------------------
            // Server connection handler
            // ------------------------
            listener.NewConnection += (e) =>
            {
                logger.WriteInfo("New connection");

                if (e.Connection is UdpServerConnection)
                {
                    logger.WriteInfo("New UDP connection");
                    server_udp_connection = e.Connection;
                }
                else if (e.Connection is WebSocketServerConnection)
                {
                    logger.WriteInfo("New WS connection");
                    server_ws_connection = e.Connection;
                }

                e.Connection.DataReceived += async (e2) =>
                {
                    logger.WriteInfo("Data received");

                    if (e.Connection is UdpServerConnection)
                    {
                        logger.WriteInfo("UDP data received");
                        serverUdpReceive.TrySetResult(true);

                        // Echo the original reliable message directly
                        var writer = new MessageWriter(allocator);
                        writer.Write(e2.Message.Buffer, 0, e2.Message.Length);
                        await e.Connection.Send(writer);
                    }
                    else if (e.Connection is WebSocketServerConnection)
                    {
                        logger.WriteInfo("WS data received");
                        serverWsReceive.TrySetResult(true);

                        // Echo exact buffer safely
                        var writer = new MessageWriter(allocator);
                        writer.Write(e2.Message.Buffer, 0, e2.Message.Length);
                        await e.Connection.Send(writer);
                    }
                };
            };

            listener.Start();

            // ------------------------
            // UDP Client
            // ------------------------
            var udp_client = new UdpClientConnection(logger, new IPEndPoint(IPAddress.Loopback, port));

            udp_client.DataReceived += async (e) =>
            {
                clientUdpReceive.TrySetResult(true);
                for (int i = 3; i < udp_data.Length; i++)
                {
                    Assert.Equal(udp_data[i], e.Message.Buffer[i]);
                }
            };

            // Connect UDP client (do NOT recycle handshake)
            var handshake = UdpMessageFactory.BuildHandshakeMessage(udp_client);
            await udp_client.Connect(handshake);

            // ------------------------
            // WebSocket Client
            // ------------------------
            var ws_client = new WebSocketClientConnection(logger);

            ws_client.DataReceived += async (e) =>
            {
                clientWsReceive.TrySetResult(true);
                var received = e.Message.ReadBytes(e.Message.Length);
                Assert.Equal(ws_data, received);
            };

            var ws_uri = $"ws://127.0.0.1:{port}";
            var ws_writer_connect = new MessageWriter(allocator);
            ws_writer_connect.Write(ws_uri);
            await ws_client.Connect(ws_writer_connect);

            // ------------------------
            // Wait for server connections
            // ------------------------
            var timeout = TimeSpan.FromSeconds(5);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while ((server_udp_connection == null || server_ws_connection == null) && sw.Elapsed < timeout)
            {
                await Task.Delay(50);
            }

            Assert.NotNull(server_udp_connection);
            Assert.NotNull(server_ws_connection);

            // ------------------------
            // Send test data
            // ------------------------
            var udp_writer = UdpMessageFactory.BuildReliableMessage(udp_client);
            udp_writer.Write(udp_data);
            await udp_client.Send(udp_writer);

            var ws_writer_send = new MessageWriter(allocator);
            ws_writer_send.Write(ws_data);
            await ws_client.Send(ws_writer_send);

            // ------------------------
            // Wait for all messages to be received
            // ------------------------
            await Task.WhenAny(serverUdpReceive.Task, Task.Delay(2000000));
            await Task.WhenAny(serverWsReceive.Task, Task.Delay(2000000));
            await Task.WhenAny(clientUdpReceive.Task, Task.Delay(2000000));
            await Task.WhenAny(clientWsReceive.Task, Task.Delay(2000000));

            // ------------------------
            // Cleanup
            // ------------------------
            await udp_client.Disconnect("test", new MessageWriter(allocator));
            await ws_client.Disconnect("test", new MessageWriter(allocator));
            listener.Dispose();
        }
    }
}
