namespace Infinity.Core.Udp
{
    internal class UdpSendOptionInternal
    {
        /// <summary>
        ///     Handshake message for initiating communication.
        /// </summary>
        public const byte Handshake = 8;

        /// <summary>
        /// A single byte of continued existence
        /// </summary>
        public const byte Ping = 12;

        /// <summary>
        ///     Message acknowledging the receipt of a message.
        /// </summary>
        public const byte Acknowledgement = 10;

        /// <summary>
        ///     Message that is part of a larger, fragmented message.
        /// </summary>
        public const byte Fragment = 11;
    }

    /// <summary>
    ///     Extra public states for SendOption enumeration when using UDP.
    /// </summary>
    public class UdpSendOption
    {
        /// <summary>
        ///     Requests unreliable delivery with no framentation.
        /// </summary>
        public const byte None = 0;

        /// <summary>
        ///     Requests data be sent reliably but with no fragmentation.
        /// </summary>
        /// <remarks>
        ///     Sending data reliably means that data is guarenteed to arrive and to arrive only once. Reliable delivery
        ///     typically requires more processing, more memory (as packets need to be stored in case they need resending), 
        ///     a larger number of protocol bytes and can be slower than unreliable delivery.
        /// </remarks>
        public const byte Reliable = 1;

        /// <summary>
        ///     Message for discontinuing communication.
        /// </summary>
        public const byte Disconnect = 9;
    }
}
