namespace Infinity.Core.Udp
{
    /// <summary>
    ///     Extra public states for SendOption enumeration when using UDP.
    /// </summary>
    public enum UdpSendOption : byte
    {
        /// <summary>
        ///     Requests unreliable delivery with no framentation.
        /// </summary>
        None = 0,

        /// <summary>
        ///     Requests data be sent reliably but with no fragmentation.
        /// </summary>
        /// <remarks>
        ///     Sending data reliably means that data is guarenteed to arrive and to arrive only once. Reliable delivery
        ///     typically requires more processing, more memory (as packets need to be stored in case they need resending), 
        ///     a larger number of protocol bytes and can be slower than unreliable delivery.
        /// </remarks>
        Reliable = 1,

        /// <summary>
        ///     Hello message for initiating communication.
        /// </summary>
        Hello = 8,

        /// <summary>
        /// A single byte of continued existence
        /// </summary>
        Ping = 12,

        /// <summary>
        ///     Message for discontinuing communication.
        /// </summary>
        Disconnect = 9,

        /// <summary>
        ///     Message acknowledging the receipt of a message.
        /// </summary>
        Acknowledgement = 10,

        /// <summary>
        ///     Message that is part of a larger, fragmented message.
        /// </summary>
        Fragment = 11,
    }
}
