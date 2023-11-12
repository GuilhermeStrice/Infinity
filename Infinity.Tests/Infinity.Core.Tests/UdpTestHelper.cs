﻿using Infinity.Core.Udp;
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
                Trace.WriteLine("Data was received correctly.");

                try
                {
                    result = a;
                }
                finally
                {
                    mutex.Set();
                }
            };

            connection.Connect();

            //Wait until data is received
            mutex.WaitOne();

            var reader = ConvertToMessageReader(data);
            Assert.Equal(reader.Length, result.Value.Message.Length);
            for (int i = reader.Offset; i < reader.Length; i++)
            {
                Assert.Equal(reader.Buffer[i], result.Value.Message.ReadByte());
            }

            Assert.Equal(sendOption, result.Value.SendOption);
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
                    Trace.WriteLine("Data was received correctly.");

                    result = innerArgs;

                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            //Connect
            connection.Connect();

            Assert.True(mutex.WaitOne(100), "Timeout while connecting");

            connection.Send(data);

            //Wait until data is received
            Assert.True(mutex2.WaitOne(100), "Timeout while sending data");

            var dataReader = ConvertToMessageReader(data);
            Assert.Equal(dataReader.Length, result.Value.Message.Length);
            for (int i = 0; i < dataReader.Length; i++)
            {
                Assert.Equal(dataReader.ReadByte(), result.Value.Message.ReadByte());
            }

            Assert.Equal(sendOption, result.Value.SendOption);
        }

        /// <summary>
        ///     Runs a server disconnect test on the given listener and connection.
        /// </summary>
        /// <param name="listener">The listener to test.</param>
        /// <param name="connection">The connection to test.</param>
        internal static void RunServerDisconnectTest(UdpConnectionListener listener, UdpConnection connection)
        {
            var mutex = new ManualResetEvent(false);

            connection.Disconnected += delegate (object sender, DisconnectedEventArgs args)
            {
                mutex.Set();
            };

            listener.NewConnection += delegate (NewConnectionEventArgs args)
            {
                args.Connection.Disconnect("Testing");
            };

            listener.Start();

            connection.Connect();

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
                args.Connection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            connection.Connect();

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
                args.Connection.Disconnected += delegate (object sender2, DisconnectedEventArgs args2)
                {
                    mutex2.Set();
                };

                mutex.Set();
            };

            listener.Start();

            connection.Connect();

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

        private static MessageReader ConvertToMessageReader(MessageWriter writer)
        {
            var output = new MessageReader();
            output.Buffer = writer.Buffer;
            output.Offset = writer.SendOption == UdpSendOption.Unreliable ? 1 : 3;
            output.Length = writer.Length - output.Offset;
            output.Position = 0;

            return output;
        }

        /// <summary>
        ///     Builds new data of increaseing value bytes.
        /// </summary>
        /// <param name="dataSize">The number of bytes to generate.</param>
        /// <returns>The data.</returns>
        static MessageWriter BuildData(byte sendOption, int dataSize)
        {
            var output = MessageWriter.Get(sendOption);
            for (int i = 0; i < dataSize; i++)
            {
                output.Write((byte)i);
            }

            return output;
        }
    }
}
