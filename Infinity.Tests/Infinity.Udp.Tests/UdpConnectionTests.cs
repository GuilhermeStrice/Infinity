using System.Net.Sockets;
using System.Net;
using Xunit.Abstractions;
using Infinity.Core;
using Infinity.Tests.Core;
using System.Threading.Tasks;

namespace Infinity.Udp.Tests
{
    public class UdpConnectionTests
    {
        ITestOutputHelper output;

        public UdpConnectionTests(ITestOutputHelper _output)
        {
            UdpTestHelper._output = _output;
            output = _output;
        }

        [Fact]
        public async Task ServerDisposeDisconnectsTest()
        {
            Console.WriteLine("ServerDisposeDisconnectsTest");

            int port = Util.GetFreePort();
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, port);

            var serverConnectedTcs = new TaskCompletionSource();
            var serverDisconnectedTcs = new TaskCompletionSource();
            var clientDisconnectedTcs = new TaskCompletionSource();

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.Configuration.KeepAliveInterval = 100;

                listener.NewConnection += (evt) =>
                {
                    serverConnectedTcs.SetResult();

                    evt.Connection.Disconnected += (et) =>
                    {
                        et.Recycle();
                        clientDisconnectedTcs.SetResult();
                    };
                };

                connection.Disconnected += (evt) =>
                {
                    evt.Recycle();
                    serverDisconnectedTcs.SetResult();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                // Dispose server and wait for events
                listener.Dispose();

                await Task.WhenAny(serverConnectedTcs.Task, Task.Delay(1000));
                await Task.WhenAny(serverDisconnectedTcs.Task, Task.Delay(1000));
                await Task.WhenAny(clientDisconnectedTcs.Task, Task.Delay(1000));

                Assert.True(serverConnectedTcs.Task.IsCompleted);
                Assert.True(serverDisconnectedTcs.Task.IsCompleted);
                Assert.False(clientDisconnectedTcs.Task.IsCompleted);
            }
        }

        [Fact]
        public async Task ClientServerDisposeDisconnectsTest()
        {
            int port = Util.GetFreePort();
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, port);

            var serverConnectedTcs = new TaskCompletionSource();
            var serverDisconnectedTcs = new TaskCompletionSource();
            var clientDisconnectedTcs = new TaskCompletionSource();

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.Configuration.KeepAliveInterval = 100;

                listener.NewConnection += (evt) =>
                {
                    serverConnectedTcs.TrySetResult();

                    evt.Connection.Disconnected += (et) =>
                    {
                        et.Recycle();
                        serverDisconnectedTcs.TrySetResult();
                    };

                    evt.Recycle();
                };

                connection.Disconnected += (et) =>
                {
                    et.Recycle();
                    clientDisconnectedTcs.TrySetResult();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                // Wait for server to register connection
                await Task.WhenAny(serverConnectedTcs.Task, Task.Delay(1000));
                Assert.True(serverConnectedTcs.Task.IsCompleted, "Server never saw connection");

                // Dispose client
                connection.Dispose();

                // Wait for server disconnect event
                await Task.WhenAny(serverDisconnectedTcs.Task, Task.Delay(1000));
                Assert.True(serverDisconnectedTcs.Task.IsCompleted, "Server never saw disconnect");

                // Wait for client disconnect event with a proper timeout
                await Task.WhenAny(clientDisconnectedTcs.Task, Task.Delay(1000));
                Assert.True(clientDisconnectedTcs.Task.IsCompleted, "Client did not fire Disconnected event");
            }
        }

        /// <summary>
        ///     Tests the fields on UdpConnection
        /// </summary>
        [Fact]
        public async Task UdpFieldTest()
        {
            Console.WriteLine("UdpFieldTest");

            int port = Util.GetFreePort();
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, port);

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.Start();

                // Build and send handshake
                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                // Verify the connection's endpoint
                Assert.Equal(ep, connection.EndPoint);

                // Since this is UDP, the UdpConnection-specific EndPoint should match the loopback + port
                Assert.Equal(new IPEndPoint(IPAddress.Loopback, port), connection.EndPoint);
            }
        }

        [Fact]
        public async Task UdpHandshakeTest()
        {
            Console.WriteLine("UdpHandshakeTest");

            int port = Util.GetFreePort();

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port)))
            {
                listener.Start();

                // Build handshake message
                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                handshake.Write(new byte[] { 1, 2, 3, 4, 5, 6 });

                // Use TaskCompletionSource for async signaling
                var tcs = new TaskCompletionSource();

                listener.HandshakeConnection = (IPEndPoint endPoint, MessageReader input, out byte[] response) =>
                {
                    response = null;

                    // Validate handshake payload
                    for (int i = 3; i < input.Length; i++)
                    {
                        Assert.Equal(input.Buffer[i], handshake.Buffer[i]);
                    }

                    tcs.TrySetResult();
                    return true;
                };

                listener.NewConnection += (NewConnectionEvent e) =>
                {
                    e.Recycle();
                };

                // Connect client
                await connection.Connect(handshake);

                // Wait asynchronously for handshake to complete (max 2.5s)
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(2500));
                Assert.Equal(tcs.Task, completedTask); // Ensure handshake completed in time

                handshake.Recycle();
            }
        }

        [Fact]
        public async Task UdpUnreliableMessageSendTest()
        {
            Console.WriteLine("UdpUnreliableMessageSendTest");

            int port = Util.GetFreePort();
            using ManualResetEvent mutex = new ManualResetEvent(false);

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port)))
            {
                MessageReader? receivedMessage = null;

                listener.NewConnection += (evt) =>
                {
                    evt.Connection.DataReceived += (dataEvt) =>
                    {
                        receivedMessage = dataEvt.Message;
                        dataEvt.Recycle(false);
                        mutex.Set();
                    };
                    evt.Recycle();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);
                handshake.Recycle();

                var writer = UdpMessageFactory.BuildUnreliableMessage();
                writer.Write(new byte[] { 1, 2, 3, 4, 5, 6 });

                // writers are consumed we need to copy it
                var data_reader = writer.ToReader();

                // Send the message multiple times
                for (int i = 0; i < 4; ++i)
                {
                    _ = connection.Send(writer);
                }

                // Wait until at least one message is received
                if (!mutex.WaitOne(5000))
                {
                    Assert.Fail("Timeout waiting for message.");
                }

                // Validate the received message
                for (int i = 0; i < data_reader.Length; i++)
                {
                    Assert.Equal(data_reader.Buffer[i], receivedMessage!.Buffer[i]);
                }

                // Recycle resources
                receivedMessage!.Recycle();
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [Fact]
        public async Task UdpIPv4ConnectionTest()
        {
            Console.WriteLine("UdpIPv4ConnectionTest");

            int port = Util.GetFreePort();

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port)))
            {
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                try
                {
                    await connection.Connect(handshake);
                }
                finally
                {
                    handshake.Recycle();
                }

                // Optional: verify connection state
                Assert.Equal(ConnectionState.Connected, connection.State);
                Assert.Equal(IPAddress.Loopback, connection.EndPoint.Address);
                Assert.Equal(port, connection.EndPoint.Port);
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [Fact]
        public async Task MixedConnectionTest()
        {
            Console.WriteLine("MixedConnectionTest");

            int port = Util.GetFreePort();

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.IPv6Any, port), IPMode.IPv6))
            {
                listener.Start();

                listener.NewConnection += (evt) =>
                {
                    Console.WriteLine("v6 connection: " + evt.Connection.GetIP4Address());
                    evt.Recycle();
                };

                // IPv4 client
                using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port)))
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    try
                    {
                        await connection.Connect(handshake);
                        Assert.Equal(ConnectionState.Connected, connection.State);
                    }
                    finally
                    {
                        handshake.Recycle();
                    }
                }

                // IPv6 client
                using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.IPv6Loopback, port), IPMode.IPv6))
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    try
                    {
                        await connection.Connect(handshake);
                        Assert.Equal(ConnectionState.Connected, connection.State);
                    }
                    finally
                    {
                        handshake.Recycle();
                    }
                }
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to non-Handshake connections.
        /// </summary>
        [Fact]
        public void FalseConnectionTest()
        {
            Console.WriteLine("FalseConnectionTest");

            int port = Util.GetFreePort();

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port)))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                int connects = 0;
                var mutex = new ManualResetEvent(false);

                listener.NewConnection += (obj) =>
                {
                    obj.Recycle();
                    connects++;
                    mutex.Set();
                };

                listener.Start();

                // Give the listener a tiny moment to start
                Thread.Sleep(50);

                socket.Bind(new IPEndPoint(IPAddress.Any, 0));

                var bytes = new byte[2];
                bytes[0] = 32;
                for (int i = 0; i < 10; ++i)
                {
                    socket.SendTo(bytes, new IPEndPoint(IPAddress.Loopback, port));
                }

                // Wait briefly for any unexpected connections
                Thread.Sleep(500);

                Assert.Equal(0, connects);
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [Fact]
        public async Task UdpIPv6ConnectionTest()
        {
            Console.WriteLine("UdpIPv6ConnectionTest");

            int port = Util.GetFreePort();

            var connectionEstablished = new TaskCompletionSource<bool>();

            using (var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.IPv6Any, port), IPMode.IPv6))
            {
                listener.NewConnection += (evt) =>
                {
                    Console.WriteLine($"Server accepted connection from {evt.Connection.EndPoint}");
                    evt.Recycle();
                    connectionEstablished.TrySetResult(true);
                };

                listener.Start();

                using (var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.IPv6Loopback, port), IPMode.IPv6))
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    await connection.Connect(handshake);
                    handshake.Recycle();

                    // Wait until the server sees the connection, or timeout
                    var completed = await Task.WhenAny(connectionEstablished.Task, Task.Delay(3000));
                    Assert.True(completed == connectionEstablished.Task, "Server did not receive connection in time");

                    Assert.Equal(ConnectionState.Connected, connection.State);
                }
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection
        /// </summary>
        [Fact]
        public async Task UdpUnreliableServerToClientTest()
        {
            Console.WriteLine("UdpUnreliableServerToClientTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            // Run the server-to-client test using the helper
            await UdpTestHelper.RunServerToClientTest(listener, connection, 10, UdpSendOption.Unreliable);
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [Fact]
        public async Task UdpReliableServerToClientTest()
        {
            Console.WriteLine("UdpReliableServerToClientTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            // Run the server-to-client test using the helper with reliable messages
            await UdpTestHelper.RunServerToClientTest(listener, connection, 10, UdpSendOption.Reliable);
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [Fact]
        public async Task UdpUnreliableClientToServerTest()
        {
            Console.WriteLine("UdpUnreliableClientToServerTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            // Run the client-to-server test using the helper with unreliable messages
            await UdpTestHelper.RunClientToServerTest(listener, connection, 10, UdpSendOption.Unreliable);
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [Fact]
        public async Task UdpReliableClientToServerTest()
        {
            Console.WriteLine("UdpReliableClientToServerTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            // Run the client-to-server test using reliable messages
            await UdpTestHelper.RunClientToServerTest(listener, connection, 10, UdpSendOption.Reliable);
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [Fact]
        public async Task PingDisconnectClientTest()
        {
            Console.WriteLine("PingDisconnectClientTest");

            int port = Util.GetFreePort();

        #if DEBUG
            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            listener.Configuration.KeepAliveInterval = 100;
            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);
            handshake.Recycle();

            // Small delay to let connection stabilize
            await Task.Delay(10);

            // Simulate 100% packet loss to trigger disconnect
            listener.TestDropRate = 1;

            // Wait long enough for keep-alive logic to detect the connection loss
            await Task.Delay(5000);

            Assert.Equal(ConnectionState.NotConnected, connection.State);
        #else
            Assert.True(true);
        #endif
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [Fact]
        public async Task KeepAliveClientTest()
        {
            Console.WriteLine("KeepAliveClientTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            listener.Configuration.KeepAliveInterval = 100;
            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);
            handshake.Recycle();

            // Wait enough time for several keep-alive packets to be sent
            await Task.Delay(1000);

            Assert.Equal(ConnectionState.Connected, connection.State);
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [Fact]
        public async Task KeepAliveServerTest()
        {
            Console.WriteLine("KeepAliveServerTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            listener.Configuration.KeepAliveInterval = 100;

            var tcs = new TaskCompletionSource<UdpConnection>();

            listener.NewConnection += evt =>
            {
                var client = (UdpConnection)evt.Connection;

                evt.Recycle();

                // Signal that the server has a new connection
                tcs.TrySetResult(client);
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);
            handshake.Recycle();

            // Wait asynchronously for the new connection event
            var clientConnection = await tcs.Task;

            // Wait a short time to allow some keep-alive packets to be exchanged
            await Task.Delay(1000);

            Assert.Equal(ConnectionState.Connected, clientConnection.State);
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [Fact]
        public async Task ClientDisconnectTest()
        {
            Console.WriteLine("ClientDisconnectTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            await UdpTestHelper.RunClientDisconnectTest(listener, connection);
        }

        /// <summary>
        ///     Test that a disconnect is sent when the client is disposed.
        /// </summary>
        [Fact]
        public async Task ClientDisconnectOnDisposeTest()
        {
            Console.WriteLine("ClientDisconnectOnDisposeTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            await UdpTestHelper.RunClientDisconnectOnDisposeTest(listener, connection);
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [Fact]
        public async Task ServerDisconnectTest()
        {
            Console.WriteLine("ServerDisconnectTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            await UdpTestHelper.RunServerDisconnectTest(listener, connection);
        }

        /// <summary>
        ///     Tests disconnection from the server
        /// </summary>
        [Fact]
        public async Task ServerExtraDataDisconnectTest()
        {
            Console.WriteLine("ServerExtraDataDisconnectTest");

            int port = Util.GetFreePort();

            using var listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, port));
            using var connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, port));

            string received = null;
            using var mutex = new ManualResetEvent(false);

            connection.Disconnected += args =>
            {
                // We don't own the message, so read the string immediately
                received = args.Message.ReadString();
                args.Recycle();
                mutex.Set();
            };

            listener.NewConnection += args =>
            {
                // Run on a background thread to avoid race conditions on loopback
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100).ConfigureAwait(false);
                    var writer = UdpMessageFactory.BuildDisconnectMessage();
                    writer.Write("Goodbye");
                    args.Connection.Disconnect("Testing", writer);
                    args.Recycle();
                });
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            await connection.Connect(handshake);
            handshake.Recycle();

            mutex.WaitOne(2500);

            Assert.NotNull(received);
            Assert.Equal("Goodbye", received);
        }
    }
}
