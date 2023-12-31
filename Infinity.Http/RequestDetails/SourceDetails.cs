namespace Infinity.Http
{
    public class SourceDetails
    {
        /// <summary>
        /// IP address of the requestor.
        /// </summary>
        public string IpAddress { get; set; } = null;

        /// <summary>
        /// TCP port from which the request originated on the requestor.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// Source details.
        /// </summary>
        public SourceDetails()
        {

        }

        /// <summary>
        /// Source details.
        /// </summary>
        /// <param name="_ip">IP address of the requestor.</param>
        /// <param name="_port">TCP port from which the request originated on the requestor.</param>
        public SourceDetails(string _ip, int _port)
        {
            if (string.IsNullOrEmpty(_ip))
            {
                throw new ArgumentNullException(nameof(_ip));
            }

            if (_port < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_port));
            }

            IpAddress = _ip;
            Port = _port;
        }
    }
}
