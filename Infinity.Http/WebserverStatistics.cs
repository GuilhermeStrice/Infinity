namespace Infinity.Http
{
    /// <summary>
    /// Webserver statistics.
    /// </summary>
    public class WebserverStatistics
    {
        /// <summary>
        /// The time at which the client or server was started.
        /// </summary>
        public DateTime StartTime
        {
            get
            {
                return _StartTime;
            }
        }

        /// <summary>
        /// The amount of time which the client or server has been up.
        /// </summary>
        public TimeSpan UpTime
        {
            get
            {
                return DateTime.Now.ToUniversalTime() - _StartTime;
            }
        }

        /// <summary>
        /// The number of payload bytes received (incoming request body).
        /// </summary>
        public long ReceivedPayloadBytes
        {
            get
            {
                return received_payload_bytes;
            }
            internal set
            {
                received_payload_bytes = value;
            }
        }

        /// <summary>
        /// The number of payload bytes sent (outgoing request body).
        /// </summary>
        public long SentPayloadBytes
        {
            get
            {
                return sent_payload_bytes;
            }
            internal set
            {
                sent_payload_bytes = value;
            }
        }

        private DateTime _StartTime = DateTime.Now.ToUniversalTime();
        private long received_payload_bytes = 0;
        private long sent_payload_bytes = 0;
        private long[] requests_by_method; // _RequestsByMethod[(int)HttpMethod.Xyz] = Count

        /// <summary>
        /// Initialize the statistics object.
        /// </summary>
        public WebserverStatistics()
        {
            // Calculating the length for _RequestsByMethod array
            int max = 0;
            foreach (var value in Enum.GetValues(typeof(HttpMethod)))
            {
                if ((int)value > max)
                {
                    max = (int)value;
                }
            }

            requests_by_method = new long[max + 1];
        }

        /// <summary>
        /// Human-readable version of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            string ret = "";

            ret +=
                Environment.NewLine + 
                "--- Statistics ---" + Environment.NewLine +
                "    Start Time     : " + StartTime.ToString() + Environment.NewLine +
                "    Up Time        : " + UpTime.ToString("h'h 'm'm 's's'") + Environment.NewLine +
                "    Received Bytes : " + ReceivedPayloadBytes.ToString("N0") + " bytes" + Environment.NewLine +
                "    Sent Bytes     : " + SentPayloadBytes.ToString("N0") + " bytes" + Environment.NewLine;

            return ret;
        }

        /// <summary>
        /// Reset statistics other than StartTime and UpTime.
        /// </summary>
        public void Reset()
        {
            Interlocked.Exchange(ref received_payload_bytes, 0);
            Interlocked.Exchange(ref sent_payload_bytes, 0);

            for (int i = 0; i < requests_by_method.Length; i++)
            {
                Interlocked.Exchange(ref requests_by_method[i], 0);
            }
        }

        /// <summary>
        /// Increment request counter.
        /// </summary>
        /// <param name="method">HTTP method.</param>
        public void IncrementRequestCounter(HttpMethod method)
        {
            Interlocked.Increment(ref requests_by_method[(int)method]);
        }

        /// <summary>
        /// Increment received payload bytes.
        /// </summary>
        /// <param name="len">Length.</param>
        public void IncrementReceivedPayloadBytes(long len)
        {
            Interlocked.Add(ref received_payload_bytes, len);
        }

        /// <summary>
        /// Increment sent payload bytes.
        /// </summary>
        /// <param name="len">Length.</param>
        public void IncrementSentPayloadBytes(long len)
        {
            Interlocked.Add(ref sent_payload_bytes, len);
        }
    }
}
