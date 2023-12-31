namespace Infinity.Http
{
    public class IOSettings
    {
        /// <summary>
        /// Buffer size to use when interacting with streams.
        /// </summary>
        public int StreamBufferSize
        {
            get
            {
                return stream_buffer_size;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(StreamBufferSize));
                }

                stream_buffer_size = value;
            }
        }

        /// <summary>
        /// Maximum number of concurrent requests.
        /// </summary>
        public int MaxRequests
        {
            get
            {
                return max_requests;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentException("Maximum requests must be greater than zero.");
                }

                max_requests = value;
            }
        }

        /// <summary>
        /// Read timeout, in milliseconds.
        /// This property is only used by WatsonWebserver.Lite.
        /// </summary>
        public int ReadTimeoutMs
        {
            get
            {
                return read_timeout_ms;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(ReadTimeoutMs));
                }

                read_timeout_ms = value;
            }
        }

        /// <summary>
        /// Maximum incoming header size, in bytes.
        /// This property is only used by WatsonWebserver.Lite.
        /// </summary>
        public int MaxIncomingHeadersSize
        {
            get
            {
                return max_incoming_headers_size;
            }
            set
            {
                if (value < 1)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaxIncomingHeadersSize));
                }

                max_incoming_headers_size = value;
            }
        }

        /// <summary>
        /// Flag indicating whether or not the server requests a persistent connection.
        /// </summary>
        public bool EnableKeepAlive { get; set; } = false;

        private int stream_buffer_size = 65536;
        private int max_requests = 1024;
        private int read_timeout_ms = 10000;
        private int max_incoming_headers_size = 65536;

        /// <summary>
        /// Input-output settings.
        /// </summary>
        public IOSettings()
        {

        }
    }
}
