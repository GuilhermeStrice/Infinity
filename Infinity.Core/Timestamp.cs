namespace Infinity.Core
{
    /// <summary>
    /// Object used to measure start, end, and total time associated with an operation.
    /// </summary>
    public class Timestamp : IDisposable
    {
        /// <summary>
        /// The time at which the operation started.
        /// </summary>
        public DateTime Start
        {
            get
            {
                return start;
            }
            set
            {
                start = value.ToUniversalTime();

                if (end != null)
                {
                    if (start > end.Value)
                    {
                        throw new ArgumentException("Start time must be before end time.");
                    }
                }
            }
        }

        /// <summary>
        /// The time at which the operation ended.
        /// </summary>
        public DateTime? End
        {
            get
            {
                return end;
            }
            set
            {
                if (value == null)
                {
                    end = null;
                }
                else
                {
                    if (value < start)
                    {
                        throw new ArgumentException("End time must be after start time.");
                    }

                    end = value.Value.ToUniversalTime();
                }
            }
        }

        /// <summary>
        /// The total number of milliseconds that transpired between Start and End.
        /// </summary>
        public double? TotalMs
        {
            get
            {
                if (end == null)
                {
                    return Math.Round(TotalMsBetween(start, DateTime.UtcNow), 2);
                }
                else
                {
                    return Math.Round(TotalMsBetween(start, end.Value), 2);
                }
            }
        }

        /// <summary>
        /// Log messages attached to the object by the user.
        /// </summary>
        public Dictionary<DateTime, string> Messages
        {
            get
            {
                lock (@lock)
                {
                    return messages;
                }
            }
        }

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        public object Metadata
        {
            get
            {
                return metadata;
            }
            set
            {
                metadata = value;
            }
        }

        private DateTime start = DateTime.UtcNow;
        private DateTime? end = null;
        private readonly object @lock = new object();
        private Dictionary<DateTime, string> messages = new Dictionary<DateTime, string>();
        private object metadata = null;
        private bool disposed = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Timestamp()
        {

        }

        /// <summary>
        /// Add a message.
        /// </summary>
        /// <param name="_msg">Message.</param>
        public void AddMessage(string _msg)
        {
            if (string.IsNullOrEmpty(_msg))
            {
                throw new ArgumentNullException(nameof(_msg));
            }

            lock (@lock)
            {
                messages.Add(DateTime.UtcNow, _msg);
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool _disposing)
        {
            if (!disposed)
            {
                end = null;
                messages = null;
                metadata = null;
                disposed = true;
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private double TotalMsBetween(DateTime _start, DateTime _end)
        {
            try
            {
                start = _start.ToUniversalTime();
                end = _end.ToUniversalTime();
                TimeSpan total = _end - _start;
                return total.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}