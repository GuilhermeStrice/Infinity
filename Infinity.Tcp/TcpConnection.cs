using System.Net;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;

namespace Infinity.Core.Tcp
{
    internal delegate void OnHandshake(MessageReader handshakeData, TcpConnection connection);

    /// <summary>
    ///     Represents a connection that uses the TCP protocol.
    /// </summary>
    public class TcpConnection : NetworkConnection
    {
        /// <summary>
        ///     The socket we're managing.
        /// </summary>
        Socket socket;
        internal event OnHandshake OnHandshake;

        public TcpConnectionStatistics Statistics { get; private set; }

        /// <summary>
        ///     Creates a TcpConnection from a given TCP Socket.
        /// </summary>
        /// <param name="socket">The TCP socket to wrap.</param>
        internal TcpConnection(Socket socket)
        {
            Statistics = new TcpConnectionStatistics();

            //Check it's a TCP socket
            if (socket.ProtocolType != ProtocolType.Tcp)
                throw new ArgumentException("A TcpConnection requires a TCP socket.");

            EndPoint = socket.RemoteEndPoint as IPEndPoint;

            this.socket = socket;

            State = ConnectionState.Connected;
        }

        /// <summary>
        ///     Creates a new TCP connection.
        /// </summary>
        /// <param name="remoteEndPoint">A <see cref="NetworkEndPoint"/> to connect to.</param>
        public TcpConnection(IPEndPoint endPoint, IPMode ipMode = IPMode.IPv4, ILogger logger = null)
        {
            Statistics = new TcpConnectionStatistics();

            if (State != ConnectionState.NotConnected)
                throw new InvalidOperationException("Cannot connect as the Connection is already connected.");

            EndPoint = endPoint;
            IPMode = ipMode;

            //socket = CreateSocket(Protocol.Tcp, ipMode);
        }

        public override void Connect(MessageWriter _writer, int timeout = 5000)
        {
            //Connect
            State = ConnectionState.Connecting;

            try
            {
                IAsyncResult result = socket.BeginConnect(EndPoint, null, null);

                result.AsyncWaitHandle.WaitOne(timeout);

                socket.EndConnect(result);

                State = ConnectionState.Connected;
            }
            catch (Exception e)
            {
                throw new InfinityException("Could not connect as an exception occured.", e);
            }

            //Start receiving data
            try
            {
                StartWaitingForHeader(BodyReadCallback);
            }
            catch (Exception e)
            {
                throw new InfinityException("An exception occured while initiating the first receive operation.", e);
            }

            SendHandshake(_writer, () =>
            {
            });
        }

        public override void ConnectAsync(MessageWriter _writer)
        {
            //Connect
            State = ConnectionState.Connecting;

            try
            {
                IAsyncResult result = socket.BeginConnect(EndPoint, HandleConnectAsync, _writer);
            }
            catch (Exception e)
            {
                throw new InfinityException("Could not connect as an exception occured.", e);
            }
        }

        public void HandleConnectAsync(IAsyncResult result)
        {
            try
            {
                socket.EndConnect(result);

                State = ConnectionState.Connected;

                //Start receiving data
                try
                {
                    StartWaitingForHeader(BodyReadCallback);
                }
                catch (Exception e)
                {
                    throw new InfinityException("An exception occured while initiating the first receive operation.", e);
                }

                SendHandshake((MessageWriter)result.AsyncState, () =>
                {
                });
            }
            catch
            {
                State = ConnectionState.NotConnected;
            }
        }

        /// <summary>
        ///     Sends a Handshake packet to the remote endpoint.
        /// </summary>
        /// <param name="acknowledgeCallback">The callback to invoke when the Handshake packet is acknowledged.</param>
        protected void SendHandshake(MessageWriter _writer, Action acknowledgeCallback)
        {
            Send(_writer);
        }

        /// <summary>
        ///     Called when a 4 byte header has been received.
        /// </summary>
        /// <param name="bytes">The 4 header bytes read.</param>
        /// <param name="callback">The callback to invoke when the body has been received.</param>
        void HeaderReadCallback(byte[] bytes, Action<byte[]> callback)
        {
            //Get length 
            int length = GetLengthFromBytes(bytes);

            //Begin receiving the body
            try
            {
                StartWaitingForBytes(length, callback);
            }
            catch (Exception e)
            {
                HandleDisconnect(new InfinityException("An exception occured while initiating a body receive operation.", e));
            }
        }

        /// <summary>
        ///     Callback for when a body has been read.
        /// </summary>
        /// <param name="bytes">The data bytes received by the connection.</param>
        void BodyReadCallback(byte[] message)
        {
            if (message[0] != TcpSendOption.MessageOrdered)
                HandleMessage(message);

            //Begin receiving from the start
            try
            {
                StartWaitingForHeader(BodyReadCallback);
            }
            catch (Exception e)
            {
                HandleDisconnect(new InfinityException("An exception occured while initiating a header receive operation.", e));
            }

            if (message[0] == TcpSendOption.MessageOrdered)
                HandleMessage(message);
        }

        /// <summary>
        ///     Helper method to invoke the data received event.
        /// </summary>
        /// <param name="sendOption">The send option the message was received with.</param>
        /// <param name="buffer">The buffer received.</param>
        /// <param name="dataOffset">The offset of data in the buffer.</param>
        void InvokeDataReceived(byte sendOption, MessageReader buffer, int dataOffset, int bytesReceived)
        {
            buffer.Length = bytesReceived - dataOffset;
            buffer.Position = 0;

            InvokeDataReceived(buffer);
        }

        void HandleMessage(byte[] message)
        {
            // check if server accepted connection

            Statistics.LogStreamSent(message.Length);

            var reader = MessageReader.Get(message);
            switch (message[0])
            {
                // Connect is only handled by the server for handshake
                case TcpSendOptionInternal.Handshake:
                    OnHandshake.Invoke(reader, this);
                    break;
                case TcpSendOptionInternal.Disconnect:
                    DisconnectRemote("The remote sent a disconnect request", reader);
                    break;
                case TcpSendOption.MessageUnordered:
                    InvokeDataReceived(TcpSendOption.MessageUnordered, reader, 1, message.Length);
                    break;
                case TcpSendOption.MessageOrdered:
                    InvokeDataReceived(TcpSendOption.MessageOrdered, reader, 1, message.Length);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        ///     Starts this connection receiving data.
        /// </summary>
        internal void StartReceiving()
        {
            try
            {
                StartWaitingForHeader(BodyReadCallback);
            }
            catch (Exception e)
            {
                HandleDisconnect(new InfinityException("An exception occured while initiating the first receive operation.", e));
            }
        }

        /// <summary>
        ///     Starts this connections waiting for the header.
        /// </summary>
        /// <param name="callback">The callback to invoke when the body has been read.</param>
        void StartWaitingForHeader(Action<byte[]> callback)
        {
            StartWaitingForBytes(4, (bytes) => HeaderReadCallback(bytes, callback));
        }

        /// <summary>
        ///     Waits for the specified amount of bytes to be received.
        /// </summary>
        /// <param name="length">The number of bytes to receive.</param>
        /// <param name="callback">The callback </param>
        void StartWaitingForBytes(int length, Action<byte[]> callback)
        {
            StateObject state = new StateObject(length, callback);

            StartWaitingForChunk(state);
        }

        /// <summary>
        ///     Waits for the next chunk of data from this socket.
        /// </summary>
        /// <param name="state">The StateObject for the receive operation.</param>
        void StartWaitingForChunk(StateObject state)
        {
            try
            {
                socket.BeginReceive(state.buffer, state.totalBytesReceived, state.buffer.Length - state.totalBytesReceived, SocketFlags.None, ChunkReadCallback, state);
            }
            catch
            {
                Dispose();
            }
        }

        /// <summary>
        ///     Called when a chunk has been read.
        /// </summary>
        /// <param name="result"></param>
        void ChunkReadCallback(IAsyncResult result)
        {
            int bytesReceived;

            //End the receive operation
            try
            {
                bytesReceived = socket.EndReceive(result);
            }
            catch (SocketException e)
            {
                DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "An exception occured while completing a chunk read operation. " + e.Message);
                return;
            }
            catch (Exception)
            {
                return;
            }

            var state = (StateObject)result.AsyncState;

            state.totalBytesReceived += bytesReceived;

            //Exit if receive nothing
            if (bytesReceived == 0)
            {
                HandleDisconnect();
                return;
            }

            //If we need to receive more then wait for more, else process it.
            if (state.totalBytesReceived < state.buffer.Length)
            {
                try
                {
                    StartWaitingForChunk(state);
                }
                catch (Exception)
                {
                    DisconnectInternal(InfinityInternalErrors.SocketExceptionReceive, "An exception occured while initiating a chunk receive operation.");
                    return;
                }
            }
            else
            {
                Statistics.LogStreamReceived(state.totalBytesReceived + 4);

                state.callback.Invoke(state.buffer);
            }
        }

        /// <summary>
        ///     Called when the socket has been disconnected at the remote host.
        /// </summary>
        /// <param name="e">The exception if one was the cause.</param>
        void HandleDisconnect(InfinityException e = null)
        {
            bool invoke = false;

            //Only invoke the disconnected event if we're not already disconnecting
            if (State == ConnectionState.Connected)
            {
                State = ConnectionState.NotConnected;
                invoke = true;
            }

            //Invoke event outide lock if need be
            if (invoke)
            {
                InvokeDisconnected(e?.Message, null);

                Dispose();
            }
        }

        /// <summary>
        ///     Appends the length header to the bytes.
        /// </summary>
        /// <param name="bytes">The source bytes.</param>
        /// <returns>The new bytes.</returns>
        static byte[] AppendLengthHeader(MessageWriter writer)
        {
            byte[] buffer;

            buffer = new byte[writer.Length + 4];

            buffer[0] = (byte)(((uint)writer.Length >> 24) & 0xFF);
            buffer[1] = (byte)(((uint)writer.Length >> 16) & 0xFF);
            buffer[2] = (byte)(((uint)writer.Length >> 8) & 0xFF);
            buffer[3] = (byte)(uint)writer.Length;

            Buffer.BlockCopy(writer.Buffer, 0, buffer, 4, writer.Length);

            return buffer;
        }

        /// <summary>
        ///     Returns the length from a length header.
        /// </summary>
        /// <param name="bytes">The bytes received.</param>
        /// <returns>The number of bytes.</returns>
        static int GetLengthFromBytes(byte[] bytes)
        {
            if (bytes.Length != 4)
                throw new IndexOutOfRangeException("Length doesn't match required to read length");

            return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                State = ConnectionState.NotConnected;

                if (socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                }

                socket.Close();
            }

            base.Dispose(disposing);
        }

        protected override bool SendDisconnect(MessageWriter data = null)
        {
            MessageWriter msg = MessageWriter.Get();
            msg.Buffer[0] = TcpSendOptionInternal.Disconnect;

            if (data != null)
            {
                Buffer.BlockCopy(data.Buffer, 0, msg.Buffer, 1, data.Length);
            }

            Send(msg);

            return true;
        }

        internal SendErrors SendBytes(byte[] buffer, Action callback = null)
        {
            try
            {
                socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, HandleSendBytes, callback);
            }
            catch (Exception e)
            {
                //logger?.WriteError("Unknown exception while sending: " + e);
                return SendErrors.Unknown;
            }

            return SendErrors.None;
        }

        public void HandleSendBytes(IAsyncResult result)
        {
            int sent = 0;

            try
            {
                sent = socket.EndSend(result);
            }
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                // Already disposed and disconnected...
            }
            catch (SocketException)
            {
                // probably disconnected
                DisconnectInternal(InfinityInternalErrors.SocketExceptionSend, "A socket exception occurred while sending");
            }

            Statistics.LogStreamSent(sent);

            var callback = (Action)result.AsyncState;
            if (callback != null)
                callback();
        }

        public override SendErrors Send(MessageWriter msg)
        {
            if (state != ConnectionState.Connected)
            {
                return SendErrors.Disconnected;
            }

            //Get bytes for length
            var buffer = AppendLengthHeader(msg);

            var res = SendBytes(buffer);
            return res;
        }

        protected override void SetState(ConnectionState _state)
        {
            throw new NotImplementedException();
        }

        protected override void DisconnectRemote(string _reason, MessageReader _reader)
        {
            if (SendDisconnect(null))
            {
                try
                {
                    InvokeDisconnected(_reason, _reader);
                }
                catch { }
            }

            Dispose();
        }

        protected override void DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
            var msg = OnInternalDisconnect?.Invoke(_error);

            Disconnect(_reason, msg);
        }
    }
}