using System.Threading.Tasks;
using Infinity.Core;
using Xunit.Abstractions;

namespace Infinity.Udp.Tests
{
    public static class UdpTestHelper
    {
        static ChunkedByteAllocator allocator = new ChunkedByteAllocator(1024);

        public static ITestOutputHelper? _output;

        /// <summary>
        ///     Runs a general test on the given listener and connection
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        public static async Task RunServerToClientTest(UdpConnectionListener listener, UdpConnection connection, int dataSize, byte sendOption)
        {
            //Setup meta stuff 
            var data = BuildData(sendOption, dataSize);
            var data_reader = data.ToReader();
            var mutex = new ManualResetEvent(false);

            //Setup listener
            listener.NewConnection += delegate (NewConnectionEvent ncArgs)
            {
                ncArgs.Connection.Send(data);
            };

            listener.Start();

            DataReceivedEvent result = new DataReceivedEvent();
            //Setup conneciton
            connection.DataReceived += async delegate (DataReceivedEvent a)
            {
                _output.WriteLine("Data was received correctly.");

                result = a;
                mutex.Set();
            };

            var handshake = UdpMessageFactory.BuildHandshakeMessage(connection);
            await connection.Connect(handshake);

            //Wait until data is received
            mutex.WaitOne(5000);

            _output.WriteLine($"Expected length: {data_reader.Length}, Actual length: {result.Message.Length}");
            _output.WriteLine($"Reader Position: {data_reader.Position}, Message Position: {result.Message.Position}");
            _output.WriteLine($"Message Buffer Length: {result.Message.Length}, Message Length: {result.Message.Length}");
            Assert.Equal(data_reader.Length, result.Message.Length);
            for (int i = data_reader.Position; i < data_reader.Length; i++)
            {
                Assert.Equal(data_reader.Buffer[i], result.Message.Buffer[i]);
            }

            Assert.Equal(sendOption, result.Message.Buffer[0]);

            connection.Dispose();
            listener.Dispose();
        }

        /// <summary>
        ///     Runs a general test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        public static async Task RunClientToServerTest(UdpConnectionListener listener, UdpConnection connection, int dataSize, byte sendOption)
        {
            //Setup meta stuff 
            var data = BuildData(sendOption, dataSize);
            var data_reader = data.ToReader();
            var mutex = new ManualResetEvent(false);
            var mutex2 = new ManualResetEvent(false);

            //Setup listener
            DataReceivedEvent result = new DataReceivedEvent();
            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.Connection.DataReceived += async delegate (DataReceivedEvent innerArgs)
                {
                    _output.WriteLine("Data was received correctly.");

                    result = innerArgs;

                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            //Connect
            var handshake = UdpMessageFactory.BuildHandshakeMessage(connection);
            await connection.Connect(handshake);

            Assert.True(mutex.WaitOne(1000), "Timeout while connecting");

            await connection.Send(data);

            //Wait until data is received
            Assert.True(mutex2.WaitOne(1000), "Timeout while sending data");

            Assert.Equal(data_reader.Length, result.Message.Length);
            for (int i = 3; i < data_reader.Length; i++)
            {
                Assert.Equal(data_reader.Buffer[i], result.Message.Buffer[i]);
            }

            Assert.Equal(sendOption, result.Message.Buffer[0]);

            connection.Dispose();
            listener.Dispose();
        }

        /// <summary>
        ///     Runs a server disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        public static async Task RunServerDisconnectTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);

            connection.Disconnected += async delegate (DisconnectedEvent args)
            {
                mutex.Set();
            };

            listener.NewConnection += async delegate (NewConnectionEvent args)
            {
                var writer = UdpMessageFactory.BuildDisconnectMessage(connection);
                await args.Connection.Disconnect("Testing", writer);
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage(connection);
            await connection.Connect(handshake);

            mutex.WaitOne(2500);

            connection.Dispose();
            listener.Dispose();
        }

        /// <summary>
        ///     Runs a client disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        public static async Task RunClientDisconnectTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);
            var mutex2 = new ManualResetEvent(false);

            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.Connection.Disconnected += async delegate (DisconnectedEvent args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage(connection);
            await connection.Connect(handshake);

            mutex.WaitOne(2500);

            var writer = UdpMessageFactory.BuildDisconnectMessage(connection);
            await connection.Disconnect("Testing", writer);

            mutex2.WaitOne(2500);

            connection.Dispose();
            listener.Dispose();
        }

        /// <summary>
        ///     Ensures a client sends a disconnect packet to the server on Dispose.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        public static async Task RunClientDisconnectOnDisposeTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);
            var mutex2 = new ManualResetEvent(false);

            listener.Configuration.KeepAliveInterval = 100;
            listener.NewConnection += delegate (NewConnectionEvent args)
            {
                args.Connection.Disconnected += async delegate (DisconnectedEvent args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            var handshake = UdpMessageFactory.BuildHandshakeMessage(connection);
            await connection.Connect(handshake);

            if (!mutex.WaitOne(TimeSpan.FromSeconds(2)))
            {
                Assert.Fail("Timeout waiting for client connection");
            }

            connection.Dispose();

            if (!mutex2.WaitOne(TimeSpan.FromSeconds(2)))
            {
                Assert.Fail("Timeout waiting for client disconnect packet");
            }

            listener.Dispose();
        }

        /// <summary>
        ///     Builds new data of increaseing value bytes.
        /// </summary>
        /// <param name="dataSize">The number of bytes to generate.</param>
        /// <returns>The data.</returns>
        private static MessageWriter BuildData(byte sendOption, int dataSize)
        {
            int offset = 2;
            if (sendOption == UdpSendOption.Unreliable)
            {
                offset = 0;
            }

            var output = new MessageWriter(allocator);
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
