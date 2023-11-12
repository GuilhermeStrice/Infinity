using Infinity.Core.Tcp;
using System.Net;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public class TcpConnectionTests
    {
        public TcpConnectionTests(ITestOutputHelper output)
        {
            TcpTestHelper._output = output;
        }

        /// <summary>
        ///     Tests the fields on TcpConnection.
        /// </summary>
        [Fact]
        public void TcpFieldTest()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 4296);

            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(ep))
            {
                listener.Start();

                connection.Connect();

                //Connection fields
                Assert.Equal(ep, connection.EndPoint);

                //TcpConnection fields
                Assert.Equal(1, connection.Statistics.StreamsSent);
                Assert.Equal(0, connection.Statistics.StreamsReceived);
            }
        }

        [Fact]
        public void TcpHandshakeTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                listener.NewConnection += delegate (NewConnectionEventArgs e)
                {
                    Assert.True(Enumerable.SequenceEqual(e.HandshakeData.Buffer, new byte[] { 1, 2, 3, 4, 5, 6 }));
                };

                connection.Connect(new byte[] { 1, 2, 3, 4, 5, 6 });
            }
        }

        /// <summary>
        ///     Tests IPv4 connectivity.
        /// </summary>
        [Fact]
        public void TcpIPv4ConnectionTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                listener.Start();

                connection.Connect();
            }
        }

        /// <summary>
        ///     Tests dual mode connectivity.
        /// </summary>
        [Fact]
        public void TcpIPv6ConnectionTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.IPv6Any, 4296), IPMode.IPv6))
            {
                listener.Start();

                using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.IPv6Loopback, 4296), IPMode.IPv6))
                {
                    connection.Connect();
                }
            }
        }

        /// <summary>
        ///     Tests sending and receiving on the TcpConnection.
        /// </summary>
        [Fact]
        public void TcpServerToClientTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                TcpTestHelper.RunServerToClientTest(listener, connection, 10, TcpSendOption.MessageUnordered);
            }
        }

        /// <summary>
        ///     Tests sending and receiving on the TcpConnection.
        /// </summary>
        [Fact]
        public void TcpClientToServerTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                TcpTestHelper.RunClientToServerTest(listener, connection, 10, TcpSendOption.MessageUnordered);
            }
        }

        /// <summary>
        ///     Tests disconnection from the client.
        /// </summary>
        [Fact]
        public void ClientDisconnectTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                TcpTestHelper.RunClientDisconnectTest(listener, connection);
            }
        }

        /// <summary>
        ///     Tests disconnection from the server.
        /// </summary>
        [Fact]
        public void ServerDisconnectTest()
        {
            using (TcpConnectionListener listener = new TcpConnectionListener(new IPEndPoint(IPAddress.Any, 4296)))
            using (TcpConnection connection = new TcpConnection(new IPEndPoint(IPAddress.Loopback, 4296)))
            {
                TcpTestHelper.RunServerDisconnectTest(listener, connection);
            }
        }
    }
}