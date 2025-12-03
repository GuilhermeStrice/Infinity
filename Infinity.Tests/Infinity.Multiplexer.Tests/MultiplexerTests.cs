using Infinity.Core;
using Infinity.Tests.Core;
using Infinity.Udp;
using Infinity.WebSockets;
using System.Net;
using Xunit;
using System.Threading;
using System;

namespace Infinity.Multiplexer.Tests
{
    public class MultiplexerTests
    {
        [Fact]
        public async Task MultiplexerTest()
        {
            var udp_data = new byte[] { 1, 2, 3 };

            int port = Util.GetFreePort();

            var logger = new TestLogger("MultiplexerTest");
            var listener = new InfinityConnectionListener(new IPEndPoint(IPAddress.Any, port), logger);

            var serverUdpReceive = new ManualResetEvent(false);
            var serverWsReceive = new ManualResetEvent(false);

            var clientUdpReceive = new ManualResetEvent(false);
            var clientWsReceive = new ManualResetEvent(false);

            NetworkConnection server_udp_connection = null;
            NetworkConnection server_ws_connection = null;

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
                        serverUdpReceive.Set();

                        var writer = UdpMessageFactory.BuildReliableMessage();;
                        writer.Write(udp_data);
                        await e.Connection.Send(writer);
                    }
                    else if (e.Connection is WebSocketServerConnection)
                    {
                        logger.WriteInfo("WS data received");
                        serverWsReceive.Set();

                        var writer = MessageWriter.Get();
                        writer.Write(e2.Message.ReadBytes(e2.Message.Length));
                        await e.Connection.Send(writer);
                    }
                };
            };

            listener.Start();

            // UDP Client
            var udp_client = new UdpClientConnection(logger, new IPEndPoint(IPAddress.Loopback, 4296));

            udp_client.DataReceived += async (e) =>
            {
                clientUdpReceive.Set();
                Assert.Equal(udp_data, e.Message.ReadBytes(e.Message.Length));
            };

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await udp_client.Connect(handshake);
            handshake.Recycle();

            // WebSocket Client
            var ws_client = new WebSocketClientConnection(logger);
            var ws_data = new byte[] { 4, 5, 6 };

            ws_client.DataReceived += async (e) =>
            {
                clientWsReceive.Set();
                Assert.Equal(ws_data, e.Message.ReadBytes(e.Message.Length));
            };

            var ws_writer_connect = MessageWriter.Get();
            ws_writer_connect.Write("ws://127.0.0.1:4296");
            await ws_client.Connect(ws_writer_connect);

            await Task.Delay(2000);

            Assert.NotNull(server_udp_connection);
            Assert.NotNull(server_ws_connection);

            var udp_writer = UdpMessageFactory.BuildReliableMessage();
            udp_writer.Write(udp_data);
            await udp_client.Send(udp_writer);

            var ws_writer_send = MessageWriter.Get();
            ws_writer_send.Write(ws_data);
            await ws_client.Send(ws_writer_send);

            Assert.True(serverUdpReceive.WaitOne(1000));
            Assert.True(serverWsReceive.WaitOne(1000));

            Assert.True(clientUdpReceive.WaitOne(1000));
            Assert.True(clientWsReceive.WaitOne(1000));

            await udp_client.Disconnect("test", MessageWriter.Get());
            await ws_client.Disconnect("test", MessageWriter.Get());
            listener.Dispose();
        }
    }
}