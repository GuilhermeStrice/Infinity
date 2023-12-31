using System.Security.Cryptography.X509Certificates;

namespace Infinity.Http
{
    /// <summary>
    /// Webserver settings.
    /// </summary>
    public class WebserverSettings
    {
        /// <summary>
        /// Hostname on which to listen.
        /// </summary>
        public string Hostname
        {
            get
            {
                return hostname;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentNullException(nameof(Hostname));
                }

                hostname = value;
            }
        }

        /// <summary>
        /// TCP port on which to listen.
        /// </summary>
        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(Port));
                }

                port = value;
            }
        }

        /// <summary>
        /// Listener prefix, of the form 'http[s]://[hostname]:[port]/.
        /// </summary>
        public string Prefix
        {
            get
            {
                string ret = "";
                if (Ssl != null && Ssl.Enable)
                {
                    ret += "https://";
                }
                else
                {
                    ret += "http://";
                }

                ret += Hostname + ":" + Port + "/";
                return ret;
            }
        }

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public IOSettings IO
        {
            get
            {
                return io;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(IO));
                }

                io = value;
            }
        }

        /// <summary>
        /// SSL settings.
        /// </summary>
        public SslSettings Ssl
        {
            get
            {
                return ssl;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Ssl));
                }

                ssl = value;
            }
        }

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public HeaderSettings Headers
        {
            get
            {
                return headers;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Headers));
                }

                headers = value;
            }
        }

        /// <summary>
        /// Access control manager, i.e. default mode of operation, permit list, and deny list.
        /// </summary>
        public AccessControlManager AccessControl
        {
            get
            {
                return access_control;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(AccessControl));
                }

                access_control = value;
            }
        }

        /// <summary>
        /// Debug logging settings.
        /// Be sure to set Events.Logger in order to receive debug messages.
        /// </summary>
        public DebugSettings Debug
        {
            get
            {
                return debug;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Debug));
                }

                debug = value;
            }
        }

        private string hostname = "localhost";
        private int port = 8000;
        private IOSettings io = new IOSettings();
        private SslSettings ssl = new SslSettings();
        private AccessControlManager access_control = new AccessControlManager(AccessControlMode.DefaultPermit);
        private DebugSettings debug = new DebugSettings();
        private HeaderSettings headers = new HeaderSettings();

        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings()
        { 

        }

        /// <summary>
        /// Webserver settings.
        /// </summary>
        /// <param name="_hostname">The hostname on which to listen.</param>
        /// <param name="_port">The port on which to listen.</param>
        /// <param name="_ssl">Enable or disable SSL.</param>
        public WebserverSettings(string _hostname, int _port, bool _ssl = false)
        {
            if (string.IsNullOrEmpty(_hostname))
            {
                _hostname = "localhost";
            }

            if (_port < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(_port));
            }

            this.ssl.Enable = _ssl;
            this.hostname = _hostname;
            this.port = _port;
        }
    }
}
