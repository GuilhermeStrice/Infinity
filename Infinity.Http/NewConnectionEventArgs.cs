namespace Infinity.Http
{
    /// <summary>
    /// Connection event arguments.
    /// </summary>
    public class NewConnectionEventArgs : EventArgs
    {
        /// <summary>
        /// Requestor IP address.
        /// </summary>
        public string Ip { get; set; } = null;

        /// <summary>
        /// Request TCP port.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="_ip">Requestor IP address.</param>
        /// <param name="_port">Request TCP port.</param>
        public NewConnectionEventArgs(string _ip, int _port)
        {
            if (string.IsNullOrEmpty(_ip))
            {
                throw new ArgumentNullException(nameof(_ip));
            }

            if (_port < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_port));
            }

            Ip = _ip;
            Port = _port;
        }
    }
}
