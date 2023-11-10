namespace Infinity.Core
{
    public struct DataReceivedEventArgs
    {
        public readonly NetworkConnection Sender;

        /// <summary>
        ///     The bytes received from the client.
        /// </summary>
        public readonly MessageReader Message;

        /// <summary>
        ///     The <see cref="SendOption"/> the data was sent with.
        /// </summary>
        public readonly byte SendOption;

        public DataReceivedEventArgs(NetworkConnection sender, MessageReader msg, byte sendOption)
        {
            Sender = sender;
            Message = msg;
            SendOption = sendOption;
        }
    }

    public class DisconnectedEventArgs : EventArgs
    {
        /// <summary>
        /// Optional disconnect reason. May be null.
        /// </summary>
        public readonly string Reason;

        /// <summary>
        /// Optional data sent with a disconnect message. May be null. 
        /// You must not recycle this. If you need the message outside of a callback, you should copy it.
        /// </summary>
        public readonly MessageReader Message;

        public DisconnectedEventArgs(string reason, MessageReader message)
        {
            Reason = reason;
            Message = message;
        }
    }

    public struct NewConnectionEventArgs
    {
        /// <summary>
        /// The data received from the client in the handshake.
        /// You must not recycle this. If you need the message outside of a callback, you should copy it.
        /// </summary>
        public readonly MessageReader HandshakeData;

        /// <summary>
        /// The <see cref="Connection"/> to the new client.
        /// </summary>
        public readonly NetworkConnection Connection;

        public NewConnectionEventArgs(MessageReader handshakeData, NetworkConnection connection)
        {
            HandshakeData = handshakeData;
            Connection = connection;
        }
    }
}
