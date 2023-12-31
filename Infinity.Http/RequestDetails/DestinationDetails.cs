using System.Net;

namespace Infinity.Http
{
    public class DestinationDetails
    {
        /// <summary>
        /// IP address to which the request was made.
        /// </summary>
        public string IpAddress { get; set; } = null;

        /// <summary>
        /// TCP port on which the request was received.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// Hostname to which the request was directed.
        /// </summary>
        public string Hostname { get; set; } = null;

        /// <summary>
        /// Hostname elements.
        /// </summary>
        public string[] HostnameElements
        {
            get
            {
                string hostname = Hostname;
                string[] ret;

                if (!string.IsNullOrEmpty(hostname))
                {
                    if (!IPAddress.TryParse(hostname, out _))
                    {
                        ret = hostname.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                        return ret;
                    }
                    else
                    {
                        ret = new string[1];
                        ret[0] = hostname;
                        return ret;
                    }
                }

                ret = new string[0];
                return ret;
            }
        }

        /// <summary>
        /// Destination details.
        /// </summary>
        public DestinationDetails()
        {
        }

        /// <summary>
        /// Source details.
        /// </summary>
        /// <param name="_ip">IP address to which the request was made.</param>
        /// <param name="_port">TCP port on which the request was received.</param>
        /// <param name="_hostname">Hostname.</param>
        public DestinationDetails(string _ip, int _port, string _hostname)
        {
            if (string.IsNullOrEmpty(_ip))
            {
                throw new ArgumentNullException(nameof(_ip));
            }

            if (_port < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_port));
            }

            if (string.IsNullOrEmpty(_hostname))
            {
                throw new ArgumentNullException(nameof(_hostname));
            }

            IpAddress = _ip;
            Port = _port;
            Hostname = _hostname;
        }
    }
}
