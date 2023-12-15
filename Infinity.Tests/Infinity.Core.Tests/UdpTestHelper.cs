using Infinity.Core.Udp;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public static class UdpTestHelper
    {
        public static ITestOutputHelper _output;

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerToClientTest(UdpConnectionListener listener, UdpConnection connection, int dataSize, byte sendOption)
        {
            //Setup meta stuff 
            var data = BuildData(sendOption, dataSize);
            var mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate (NewConnectionEventArgs ncArgs)
            {
                ncArgs.Connection.Send(data);
            };

            listener.Start();

            DataReceivedEventArgs? result = null;
            //Setup conneciton
            connection.DataReceived += delegate (DataReceivedEventArgs a)
            {
                _output.WriteLine("Data was received correctly.");

                result = a;
                mutex.Set();
            };

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);

            //Wait until data is received
            mutex.WaitOne();

            var reader = data.ToReader();
            Assert.Equal(reader.Length, result.Value.Message.Length); // + 3 to account for sendOption and reliable id
            for (int i = reader.Offset; i < reader.Length; i++)
            {
                Assert.Equal(reader.Buffer[i], result.Value.Message.ReadByte());
            }

            Assert.Equal(sendOption, result.Value.Message.Buffer[0]);
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientToServerTest(UdpConnectionListener listener, UdpConnection connection, int dataSize, byte sendOption)
        {
            //Setup meta stuff 
            var data = BuildData(sendOption, dataSize);
            var mutex = new ManualResetEvent(false);
            var mutex2 = new ManualResetEvent(false);

            //Setup listener
            DataReceivedEventArgs? result = null;
            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.DataReceived += delegate (DataReceivedEventArgs innerArgs)
                {
                    _output.WriteLine("Data was received correctly.");

                    result = innerArgs;

                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            //Connect
            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);

            Assert.True(mutex.WaitOne(100), "Timeout while connecting");

            connection.Send(data);

            //Wait until data is received
            Assert.True(mutex2.WaitOne(100), "Timeout while sending data");

            var dataReader = data.ToReader();

            // account for sendOption and id
            if (sendOption != UdpSendOption.Unreliable)
            {
                dataReader.Position += 3;
                result.Value.Message.Position += 3;
            }

            Assert.Equal(dataReader.Length, result.Value.Message.Length);
            for (int i = 0; i < dataReader.Length; i++)
            {
                Assert.Equal(dataReader.ReadByte(), result.Value.Message.ReadByte());
            }

            Assert.Equal(sendOption, result.Value.Message.Buffer[0]);
        }

        /// <summary>
        ///     Runs a server disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerDisconnectTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);

            connection.Disconnected += delegate (DisconnectedEventArgs args)
            {
                mutex.Set();
            };

            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.Disconnect("Testing");
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);

            mutex.WaitOne();
        }

        /// <summary>
        ///     Runs a client disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientDisconnectTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);
            var mutex2 = new ManualResetEvent(false);

            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.Disconnected += delegate (DisconnectedEventArgs args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);

            mutex.WaitOne();

            connection.Disconnect("Testing");

            mutex2.WaitOne();
        }

        /// <summary>
        ///     Ensures a client sends a disconnect packet to the server on Dispose.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunClientDisconnectOnDisposeTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);
            var mutex2 = new ManualResetEvent(false);

            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.Disconnected += delegate (DisconnectedEventArgs args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);

            if (!mutex.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Assert.Fail("Timeout waiting for client connection");
            }

            connection.Dispose();

            if (!mutex2.WaitOne(TimeSpan.FromSeconds(1)))
            {
                Assert.Fail("Timeout waiting for client disconnect packet");
            }
        }

        /// <summary>
        ///     Builds new data of increaseing value bytes.
        /// </summary>
        /// <param name="dataSize">The number of bytes to generate.</param>
        /// <returns>The data.</returns>
        static MessageWriter BuildData(byte sendOption, int dataSize)
        {
            int offset = 2;
            if (sendOption == UdpSendOption.Unreliable)
            {
                offset = 0;
            }

            var output = MessageWriter.Get();
            output.Write(sendOption);
            output.Position += offset;
            for (int i = 0; i < dataSize; i++)
            {
                output.Write((byte)i);
            }

            return output;
        }
    }
}
