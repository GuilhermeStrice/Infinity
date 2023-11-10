namespace Infinity.Core.Tcp
{
    /// <summary>
    ///     Represents the state of the current receive operation for TCP connections.
    /// </summary>
    internal struct StateObject
    {
        /// <summary>
        ///     The buffer we're receiving.
        /// </summary>
        internal byte[] buffer;

        /// <summary>
        ///     The total number of bytes received so far.
        /// </summary>
        internal int totalBytesReceived;

        /// <summary>
        ///     The callback to invoke once the buffer has been filled.
        /// </summary>
        internal Action<byte[]> callback;

        /// <summary>
        ///     Creates a StateObject with the specified length.
        /// </summary>
        /// <param name="length">The number of bytes expected to be received.</param>
        /// <param name="callback">The callback to invoke once data has been received.</param>
        internal StateObject(int length, Action<byte[]> callback)
        {
            buffer = new byte[length];
            totalBytesReceived = 0;
            this.callback = callback;
        }
    }
}