using Infinity.Core;
using Infinity.Tests.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Diagnostics;
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

            int port = Util.GetFreePort();
            var ep = new IPEndPoint(IPAddress.Loopback, port);

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

                _ = connection.Connect(handshake);
                connections.Push(connection);
            }

            Thread.Sleep(3000); // wait events
        }

        // leaving it like this because it needs the Infinity.Udp.TestListener running
        // [Fact]
        public void StressTestMessages()
        {
            int port = Util.GetFreePort();
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, port);

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
                    _ = connection.Send(message);
                }

                message.Recycle();

                Thread.Sleep(3000); // wait events
            }
        }

        [Fact]
        public async Task StressOpeningConnections()
        {
            Console.WriteLine("StressOpeningConnections");

            int connections_to_test = 100;
            int port = Util.GetFreePort();
            var ep = new IPEndPoint(IPAddress.Loopback, port);
            ConcurrentStack<UdpClientConnection> connections = new ConcurrentStack<UdpClientConnection>();
            int con_count = 0;
            var allConnectedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            using (var listener = new UdpConnectionListener(ep))
            {
                listener.NewConnection += obj =>
                {
                    Interlocked.Increment(ref con_count);

                    obj.Connection.DataReceived += data_args =>
                    {
                        data_args.Recycle();
                    };

                    obj.Connection.Disconnected += e =>
                    {
                        e.Recycle();
                    };

                    obj.Recycle();

                    if (con_count == connections_to_test)
                    {
                        allConnectedTcs.TrySetResult();
                    }
                };

                listener.Start();

                // Launch all client connections
                var tasks = new List<Task>();
                for (int i = 0; i < connections_to_test; i++)
                {
                    var handshake = UdpMessageFactory.BuildHandshakeMessage();
                    handshake.Write(new byte[5]); // add extra bytes if needed

                    var connection = new UdpClientConnection(new TestLogger(), ep);
                    connection.DataReceived += data_args => data_args.Recycle();
                    connection.Disconnected += e => e.Recycle();

                    tasks.Add(connection.Connect(handshake));
                    connections.Push(connection);

                    // same problem as with stress test reliable messages
                    await Task.Delay(1);
                }

                // Wait for all clients to finish connecting
                var completed = await Task.WhenAny(allConnectedTcs.Task, Task.Delay(10000));
                Assert.True(allConnectedTcs.Task.IsCompleted, "Not all connections established in time.");

                // Verify listener connection count
                Assert.Equal(connections_to_test, listener.ConnectionCount);

                // Optionally clean up connections
                foreach (var c in connections)
                {
                    c.Dispose();
                }
            }
        }

        [Fact]
        public async Task StressReliableMessages()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            Console.WriteLine("StressReliableMessages");

            int port = Util.GetFreePort();

            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, port);

            int count = 0;

            int messages_to_try = 100;

            var mutex = new ManualResetEvent(false);

            using (UdpConnectionListener listener = new UdpConnectionListener(ep))
            using (UdpConnection connection = new UdpClientConnection(new TestLogger("Client"), ep))
            {
                listener.NewConnection += (evt) =>
                {
                    evt.Connection.Disconnected += delegate (DisconnectedEvent obj)
                    {
                        obj.Recycle();
                    };

                    evt.Connection.DataReceived += async delegate (DataReceivedEvent obj)
                    {
                        count++;
                        if (count == messages_to_try)
                        {
                            mutex.Set();
                            sw.Stop();
                            Console.WriteLine("Readers: " + Core.Pools.ReaderPool.InUse.ToString());
                            Console.WriteLine("Packets: " + Infinity.Udp.Pools.PacketPool.InUse.ToString());
                            Console.WriteLine("Fragmented: " + Infinity.Udp.Pools.FragmentedMessagePool.InUse.ToString());
                            Console.WriteLine("Writers: " + Core.Pools.WriterPool.InUse.ToString());

                            Console.WriteLine("DataReceived: " + Core.Pools.DataReceivedEventPool.InUse.ToString());
                            Console.WriteLine("Disconnected: " + Core.Pools.DisconnectedEventPool.InUse.ToString());
                            Console.WriteLine("NewConnection: " + Core.Pools.NewConnectionPool.InUse.ToString());

                            Console.WriteLine("Server packets: " + ((UdpConnection)(obj.Connection)).reliable_data_packets_sent.Count);
                            Console.WriteLine("Client packets: " + ((UdpConnection)connection).reliable_data_packets_sent.Count);
                        }
                        obj.Recycle();
                    };

                    evt.Recycle();
                };

                connection.Disconnected += delegate (DisconnectedEvent obj)
                {
                    obj.Recycle();
                };

                listener.Start();

                var handshake = UdpMessageFactory.BuildHandshakeMessage();
                await connection.Connect(handshake);

                sw.Start();

                for (int i = 0; i < messages_to_try; i++)
                {
                    var message = UdpMessageFactory.BuildReliableMessage();
                    message.Write(123);

                    _ = connection.Send(message);

                    // if we dont have this delay something weird happens and the packets and readers are not recycled, need to figure stuff out
                    await Task.Delay(1);
                }

                mutex.WaitOne(2000);
                Assert.Equal(messages_to_try, count);
                await Task.Delay(1000);
                Console.WriteLine($"StressReliableMessages took {sw.ElapsedMilliseconds}ms");
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
