﻿using Infinity.Udp;
using System.Net.Sockets;
using System.Net;
using Xunit.Abstractions;
using Infinity.Core;
using Infinity.Core.Tests;

namespace Infinity.Tests.Udp
{
    public class UdpConnectionTests
    {
        public UdpConnectionTests(ITestOutputHelper output)
        {
            UdpTestHelper._output = output;
        }

        [Fact]
        public void ServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.NewConnection += (evt) =>
                {
                    evt.HandshakeData.Recycle();
                    serverConnected = true;
                    evt.Connection.Disconnected += (et) =>
                    {
                        et.Message.Recycle();
                        clientDisconnected = true;
                    };
                };
                connection.Disconnected += (evt) =>
                {
                    evt.Message.Recycle();
                    serverDisconnected = true;
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.KeepAliveInterval = 100;
                connection.Connect(handshake);

                handshake.Recycle();

                Thread.Sleep(1500); // Gotta wait for the server to set up the events.
                listener.Dispose();
                Thread.Sleep(1500);

                Assert.True(serverConnected);
                Assert.True(serverDisconnected);
                Assert.False(clientDisconnected);

                connection.Dispose();
            }
        }

        [Fact]
        public void ClientServerDisposeDisconnectsTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            bool serverConnected = false;
            bool serverDisconnected = false;
            bool clientDisconnected = false;

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.NewConnection += (evt) =>
                {
                    serverConnected = true;
                    evt.HandshakeData.Recycle();
                    evt.Connection.Disconnected += (et) =>
                    {
                        et.Message.Recycle();
                        serverDisconnected = true;
                    };
                };

                connection.Disconnected += (et) =>
                {
                    et.Message.Recycle();
                    clientDisconnected = true;
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                Thread.Sleep(100); // Gotta wait for the server to set up the events.
                connection.Dispose();

                Thread.Sleep(100);

                Assert.True(serverConnected);
                Assert.True(serverDisconnected);
                Assert.False(clientDisconnected);
            }
        }

        /// <summary>
        ///     Tests the fields on UdpConnection
        /// </summary>
        [Fact]
        public void UdpFieldTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                //Connection fields
                Assert.Equal(ep, connection.EndPoint);

                //UdpConnection fields
                Assert.Equal(new IPEndPoint(IPAddress.Loopback, 4296), connection.EndPoint);
            }
        }

        [Fact]
        public void UdpHandshakeTest()
        {
            var mutex = new ManualResetEvent(false);

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                handshake.Write(new byte[] { 1, 2, 3, 4, 5, 6 });

                listener.HandshakeConnection = delegate (IPEndPoint endPoint, MessageReader input, out byte[] response)
                {
                    for (int i = 3; i < input.Length; i++)
                    {
                        Assert.Equal(input.Buffer[i], handshake.Buffer[i]);
                    }

                    response = null;
                    mutex.Set();
                    return true;
                };

                listener.NewConnection += delegate (NewConnectionEvent e)
                {
                    e.HandshakeData.Recycle();
                };
                
                connection.Connect(handshake);

                mutex.WaitOne(2500);

                handshake.Recycle();
            }
        }

        [Fact]
        public void UdpUnreliableMessageSendTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                MessageReader output = null;
                listener.NewConnection += delegate (NewConnectionEvent e)
                {
                    e.HandshakeData.Recycle();
                    e.Connection.DataReceived += delegate (DataReceivedEvent evt)
                    {
                        output = evt.Message;
                    };
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);

                var writer = UdpMessageFactory.BuildUnreliableMessage();
                writer.Write(new byte[] { 1, 2, 3, 4, 5, 6 });

                for (int i = 0; i < 4; ++i)
                {
                    connection.Send(writer);
                }

                writer.Recycle();

                Thread.Sleep(10);
                for (int i = 0; i < writer.Length; ++i)
                {
                    Assert.Equal(writer.Buffer[i], output.Buffer[i]);
                }

                handshake.Recycle();
                output.Recycle();
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [Fact]
        public void UdpIPv4ConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);

                handshake.Recycle();
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [Fact]
        public void MixedConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.IPv6Any, 4296), IPMode.IPv6))
            {
                listener.Start();

                listener.NewConnection += (evt) =>
                {
                    evt.HandshakeData.Recycle();
                    Console.WriteLine("v6 connection: " + evt.Connection.GetIP4Address());
                };

                using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296)))
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    connection.Connect(handshake);

                    Assert.Equal(ConnectionState.Connected, connection.State);
                    handshake.Recycle();
                }

                using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.IPv6Loopback, 4296), IPMode.IPv6))
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    connection.Connect(handshake);

                    Assert.Equal(ConnectionState.Connected, connection.State);

                    handshake.Recycle();
                }
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to non-Handshake connections.
        /// </summary>
        [Fact]
        public void FalseConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                int connects = 0;
                listener.NewConnection += (obj) =>
                {
                    obj.HandshakeData.Recycle();
                    Interlocked.Increment(ref connects);
                };

                listener.Start();

                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                var bytes = new byte[2];
                bytes[0] = 32;
                for (int i = 0; i < 10; ++i)
                {
                    socket.SendTo(bytes, new IPEndPoint(IPAddress.Loopback, 4296));
                }

                Thread.Sleep(500);

                Assert.Equal(0, connects);
            }
        }

        /// <summary>
        ///     Tests IPv4 resilience to multiple Handshakes.
        /// </summary>
        [Fact]
        public void ConnectLikeAJerkTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                int connects = 0;
                listener.NewConnection += (obj) =>
                {
                    obj.HandshakeData.Recycle();
                    Interlocked.Increment(ref connects);
                };

                listener.Start();

                socket.Bind(new IPEndPoint(IPAddress.Any, 0));
                var bytes = new byte[2];
                bytes[0] = 1;
                for (int i = 0; i < 10; ++i)
                {
                    socket.SendTo(bytes, new IPEndPoint(IPAddress.Loopback, 4296));
                }

                Thread.Sleep(500);

                Assert.Equal(1, connects);
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [Fact]
        public void UdpIPv6ConnectionTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.IPv6Any, 4296), IPMode.IPv6))
            {
                listener.Start();

                using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Parse("127.0.0.1"), 4296), IPMode.IPv6))
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    connection.Connect(handshake);
                    handshake.Recycle();
                }
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection
        /// </summary>
        [Fact]
        public void UdpUnreliableServerToClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunServerToClientTest(listener, connection, 10, UdpSendOption.Unreliable);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [Fact]
        public void UdpReliableServerToClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunServerToClientTest(listener, connection, 10, UdpSendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests server to client unreliable communication on the UdpConnection.
        /// </summary>
        [Fact]
        public void UdpUnreliableClientToServerTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunClientToServerTest(listener, connection, 10, UdpSendOption.Unreliable);
            }
        }

        /// <summary>
        ///     Tests server to client reliable communication on the UdpConnection.
        /// </summary>
        [Fact]
        public void UdpReliableClientToServerTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunClientToServerTest(listener, connection, 10, UdpSendOption.Reliable);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [Fact]
        public void PingDisconnectClientTest()
        {
#if DEBUG
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                // After connecting, quietly stop responding to all messages to fake connection loss.
                Thread.Sleep(10);
                listener.TestDropRate = 1;

                connection.KeepAliveInterval = 100;

                Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                Assert.Equal(ConnectionState.NotConnected, connection.State);
            }
#else
            Assert.True(true);
#endif
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [Fact]
        public void KeepAliveClientTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                connection.KeepAliveInterval = 100;
                handshake.Recycle();

                Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                Assert.Equal(ConnectionState.Connected, connection.State);
            }
        }

        /// <summary>
        ///     Tests the keepalive functionality from the client,
        /// </summary>
        [Fact]
        public void KeepAliveServerTest()
        {
            ManualResetEvent mutex = new ManualResetEvent(false);

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpConnection client = null;
                listener.NewConnection += delegate (NewConnectionEvent args)
                {
                    args.HandshakeData.Recycle();
                    client = (UdpConnection)args.Connection;
                    client.KeepAliveInterval = 100;

                    Thread.Sleep(1050);    //Enough time for ~10 keep alive packets

                    mutex.Set();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);
                mutex.WaitOne(500);

                Assert.Equal(ConnectionState.Connected, client.State);
            }
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [Fact]
        public void ClientDisconnectTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunClientDisconnectTest(listener, connection);
            }
        }

        /// <summary>
        ///     Test that a disconnect is sent when the client is disposed.
        /// </summary>
        [Fact]
        public void ClientDisconnectOnDisposeTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunClientDisconnectOnDisposeTest(listener, connection);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [Fact]
        public void ServerDisconnectTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                UdpTestHelper.RunServerDisconnectTest(listener, connection);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server
        /// </summary>
        [Fact]
        public void ServerExtraDataDisconnectTest()
        {
            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                string received = null;
                ManualResetEvent mutex = new ManualResetEvent(false);

                connection.Disconnected += delegate (DisconnectedEvent args)
                {
                    // We don't own the message, we have to read the string now 
                    received = args.Message.ReadString();
                    args.Message.Recycle();
                    mutex.Set();
                };

                listener.NewConnection += delegate (NewConnectionEvent args)
                {
                    // As it turns out, the UdpConnectionListener can have an issue on loopback where the disconnect can happen before the Handshake confirm
                    // Tossing it on a different thread makes this test more reliable. Perhaps something to think about elsewhere though.
                    Task.Run(async () =>
                    {
                        await Task.Delay(100);
                        MessageWriter writer = UdpMessageFactory.BuildDisconnectMessage();
                        writer.Write("Goodbye");
                        args.Connection.Disconnect("Testing", writer);
                        writer.Recycle();
                    });
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                mutex.WaitOne(2500);

                Assert.NotNull(received);
                Assert.Equal("Goodbye", received);
            }
        }
    }
}