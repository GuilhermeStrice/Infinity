using Infinity.Core;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public static class UdpTestHelper
    {
        public static ITestOutputHelper? _output;

        /// <summary>
        ///     Runs a general test on the given listener and connection
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerToClientTest(UdpConnectionListener listener, UdpConnection connection, int dataSize, byte sendOption)
        {
            //Setup meta stuff 
            var data = BuildData(sendOption, dataSize);
            var mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate (NewConnectionEvent ncArgs)
            {
                ncArgs.HandshakeData.Recycle();
                ncArgs.Connection.Send(data);
            };

            listener.Start();

            DataReceivedEvent? result = null;
            //Setup conneciton
            connection.DataReceived += delegate (DataReceivedEvent a)
            {
                _output.WriteLine("Data was received correctly.");

                result = a;
                mutex.Set();
            };

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);
            handshake.Recycle();

            //Wait until data is received
            mutex.WaitOne(5000);

            var reader = data.ToReader();
            Assert.Equal(reader.Length, result.Message.Length);
            for (int i = reader.Position; i < reader.Length; i++)
            {
                Assert.Equal(reader.Buffer[i], result.Message.Buffer[i]);
            }

            Assert.Equal(sendOption, result.Message.Buffer[0]);

            result.Message.Recycle();

            connection.Dispose();
            listener.Dispose();
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
            DataReceivedEvent? result = null;
            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.HandshakeData.Recycle();
                args.Connection.DataReceived += delegate (DataReceivedEvent innerArgs)
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
            handshake.Recycle();

            Assert.True(mutex.WaitOne(1000), "Timeout while connecting");

            connection.Send(data);

            //Wait until data is received
            Assert.True(mutex2.WaitOne(1000), "Timeout while sending data");

            var dataReader = data.ToReader();

            Assert.Equal(dataReader.Length, result.Message.Length);
            for (int i = 3; i < dataReader.Length; i++)
            {
                Assert.Equal(dataReader.Buffer[i], result.Message.Buffer[i]);
            }

            Assert.Equal(sendOption, result.Message.Buffer[0]);

            result.Message.Recycle();

            connection.Dispose();
            listener.Dispose();
        }

        /// <summary>
        ///     Runs a server disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerDisconnectTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);

            connection.Disconnected += delegate (DisconnectedEvent args)
            {
                args.Message.Recycle();
                mutex.Set();
            };

            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.HandshakeData.Recycle();
                var writer = UdpMessageFactory.BuildDisconnectMessage();
                args.Connection.Disconnect("Testing", writer);
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);
            handshake.Recycle();

            mutex.WaitOne(2500);

            connection.Dispose();
            listener.Dispose();
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

            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.HandshakeData.Recycle();
                args.Connection.Disconnected += delegate (DisconnectedEvent args2)
                {
                    args2.Message.Recycle();
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);
            handshake.Recycle();

            mutex.WaitOne(2500);

            var writer = UdpMessageFactory.BuildDisconnectMessage();
            connection.Disconnect("Testing", writer);
            writer.Recycle();

            mutex2.WaitOne(2500);

            connection.Dispose();
            listener.Dispose();
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

            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.HandshakeData.Recycle();
                args.Connection.Disconnected += delegate (DisconnectedEvent args2)
                {
                    args2.Message.Recycle();
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage();
            connection.Connect(handshake);
            handshake.Recycle();

            if (!mutex.WaitOne(TimeSpan.FromSeconds(2)))
            {
                Assert.Fail("Timeout waiting for client connection");
            }

            connection.Dispose();

            if (!mutex2.WaitOne(TimeSpan.FromSeconds(2)))
            {
                Assert.Fail("Timeout waiting for client disconnect packet");
            }

            connection.Dispose();
            listener.Dispose();
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
