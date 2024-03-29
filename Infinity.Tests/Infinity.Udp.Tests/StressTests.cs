﻿using Infinity.Core;
using Infinity.Tests.Core;
using System.Collections.Concurrent;
using System.Net;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public class StressTests
    {
        ITestOutputHelper output;

        public StressTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        // leaving it like this because it needs the Infinity.Udp.TestListener running
        // [Fact]
        public void StressTestConnections()
        {
            int connection_count = 50;
            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            handshake.Write(new byte[5]);

            ConcurrentStack<UdpClientConnection> connections = new ConcurrentStack<UdpClientConnection>();

            var ep = new IPEndPoint(IPAddress.Loopback, 22023);

            for (int i = 0; i < connection_count; i++)
            {
                var connection = new UdpClientConnection(new TestLogger(), ep);
                connection.DataReceived += delegate (DataReceivedEvent obj)
                {
                    obj.Recycle();
                };
                connection.Disconnected += delegate (DisconnectedEvent obj)
                {
                    obj.Recycle();
                };

                connection.Connect(handshake);
                connections.Push(connection);
            }

            Thread.Sleep(3000); // wait events
        }

        // leaving it like this because it needs the Infinity.Udp.TestListener running
        // [Fact]
        public void StressTestMessages()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 22023);

            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                connection.Disconnected += delegate (DisconnectedEvent obj)
                {
                    obj.Recycle();
                };

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                var message = UdpMessageFactory.BuildReliableMessage();
                message.Write(123);

                for (int i = 0; i < 10000; i++)
                {
                    connection.Send(message);
                }

                message.Recycle();

                Thread.Sleep(3000); // wait events
            }
        }

        [Fact]
        public void StressTestOpeningConnections()
        {
            Console.WriteLine("StressTestOpeningConnections");

            int connections_to_test = 100;
            ManualResetEvent mutex = new ManualResetEvent(false);

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            handshake.Write(new byte[5]);

            var ep = new IPEndPoint(IPAddress.Loopback, 22023);
            ConcurrentStack<UdpClientConnection> connections = new ConcurrentStack<UdpClientConnection>();
            using (var listener = new UdpConnectionListener(ep))
            {
                int con_count = 0;

                listener.NewConnection += delegate (NewConnectionEvent obj)
                {
                    con_count++;
                    obj.Connection.DataReceived += delegate (DataReceivedEvent data_args)
                    {
                        data_args.Recycle();
                    };

                    obj.Connection.Disconnected += delegate (DisconnectedEvent e)
                    {
                        e.Recycle();
                    };

                    obj.Recycle();

                    if (con_count == connections_to_test)
                    {
                        mutex.Set();
                        Assert.Equal(con_count, listener.ConnectionCount);
                    }
                };

                listener.Start();

                for (int i = 0; i < connections_to_test; i++)
                {
                    var connection = new UdpClientConnection(new TestLogger(), ep);
                    connection.DataReceived += delegate (DataReceivedEvent obj)
                    {
                        obj.Recycle();
                    };
                    connection.Disconnected += delegate (DisconnectedEvent obj)
                    {
                        obj.Recycle();
                    };

                    connection.Connect(handshake);
                    connections.Push(connection);
                }

                mutex.WaitOne(10000);

                // wait for all events to process
                handshake.Recycle();
            }
        }

        [Fact]
        public void StressReliableMessages()
        {
            Console.WriteLine("StressReliableMessages");

            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            int count = 0;

            var mutex = new ManualResetEvent(false);

            using (UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.NewConnection += (evt) =>
                {
                    evt.Connection.Disconnected += delegate (DisconnectedEvent obj)
                    {
                        obj.Recycle();
                    };

                    evt.Connection.DataReceived += delegate (DataReceivedEvent obj)
                    {
                        count++;
                        obj.Recycle();
                        if (count == 200)
                        {
                            output.WriteLine(Core.Pools.ReaderPool.InUse.ToString());
                            output.WriteLine(Infinity.Udp.Pools.PacketPool.InUse.ToString());
                            output.WriteLine(Infinity.Udp.Pools.FragmentedMessagePool.InUse.ToString());
                            output.WriteLine(Core.Pools.WriterPool.InUse.ToString());

                            output.WriteLine(Core.Pools.DataReceivedEventPool.InUse.ToString());
                            output.WriteLine(Core.Pools.DisconnectedEventPool.InUse.ToString());
                            output.WriteLine(Core.Pools.NewConnectionPool.InUse.ToString());
                            mutex.Set();
                        }
                    };

                    evt.Recycle();
                };

                connection.Disconnected += delegate (DisconnectedEvent obj)
                {
                    obj.Recycle();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                connection.Connect(handshake);
                handshake.Recycle();

                var message = UdpMessageFactory.BuildReliableMessage();
                message.Write(123);

                for (int i = 0; i < 200; i++)
                {
                    connection.Send(message);
                }

                message.Recycle();

                mutex.WaitOne(10000);
            }
        }

        // This was a thing that happened to us a DDoS. Mildly instructional that we straight up ignore it.
        /*public void SourceAmpAttack()
        {
            var localEp = new IPEndPoint(IPAddress.Any, 11710);
            var serverEp = new IPEndPoint(IPAddress.Loopback, 11710);
            using (ThreadLimitedUdpConnectionListener listener = new ThreadLimitedUdpConnectionListener(4, localEp, new ConsoleLogger(true)))
            {
                listener.Start();

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.DontFragment = false;

                try
                {
                    const int SIO_UDP_CONNRESET = -1744830452;
                    socket.IOControl(SIO_UDP_CONNRESET, new byte[1], null);
                }
                catch { } // Only necessary on Windows

                string byteAsHex = "f23c 92d1 c277 001b 54c2 50c1 0800 4500 0035 7488 0000 3b11 2637 062f ac75 2d4f 0506 a7ea 5607 0021 5e07 ffff ffff 5453 6f75 7263 6520 456e 6769 6e65 2051 7565 7279 00";
                byte[] bytes = StringToByteArray(byteAsHex.Replace(" ", ""));
                socket.SendTo(bytes, serverEp);

                while (socket.Poll(50000, SelectMode.SelectRead))
                {
                    byte[] buffer = new byte[1024];
                    int len = socket.Receive(buffer);
                    Console.WriteLine($"got {len} bytes: " + string.Join(" ", buffer.Select(b => b.ToString("X"))));
                    Console.WriteLine($"got {len} bytes: " + string.Join(" ", buffer.Select(b => (char)b)));
                }
            }
        }*/

        public static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length / 2)
                             .Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16))
                             .ToArray();
        }
    }
}
