namespace Infinity.Core
{
    /// <summary>
    ///     Represents the IP version that a connection or listener will use.
    /// </summary>
    public enum IPMode
    {
        IPv4,
        IPv6
    }

    /// <summary>
    ///     Represents the state a Connection is currently in.
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        ///     The Connection has either not been established yet or has been disconnected.
        /// </summary>
        NotConnected,

        /// <summary>
        ///     The Connection is currently connecting to an endpoint.
        /// </summary>
        Connecting,

        /// <summary>
        ///     The Connection is connected and data can be transfered.
        /// </summary>
        Connected,
    }

    public enum SendErrors
    {
        None,
        Disconnected,
        Unknown
    }

    public enum InfinityInternalErrors
    {
        SocketExceptionSend,
        SocketExceptionReceive,
        ReceivedZeroBytes,
        PingsWithoutResponse,
        ReliablePacketWithoutResponse,
        ConnectionDisconnected
    }

    public enum Protocol : byte
    {
        Udp,
        Tcp,
    }
}
