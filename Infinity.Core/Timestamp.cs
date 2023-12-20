﻿namespace Infinity.Core
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
                return _Start;
            }
            set
            {
                _Start = value.ToUniversalTime();

                if (_End != null)
                {
                    if (_Start > _End.Value) throw new ArgumentException("Start time must be before end time.");
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
                return _End;
            }
            set
            {
                if (value == null)
                {
                    _End = null;
                }
                else
                {
                    if (value < _Start) throw new ArgumentException("End time must be after start time.");
                    _End = value.Value.ToUniversalTime();
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
                if (_End == null)
                {
                    return Math.Round(TotalMsBetween(_Start, DateTime.UtcNow), 2);
                }
                else
                {
                    return Math.Round(TotalMsBetween(_Start, _End.Value), 2);
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
                lock (_Lock)
                {
                    return _Messages;
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
                return _Metadata;
            }
            set
            {
                _Metadata = value;
            }
        }

        private DateTime _Start = DateTime.UtcNow;
        private DateTime? _End = null;
        private readonly object _Lock = new object();
        private Dictionary<DateTime, string> _Messages = new Dictionary<DateTime, string>();
        private object _Metadata = null;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Timestamp()
        {

        }

        /// <summary>
        /// Add a message.
        /// </summary>
        /// <param name="msg">Message.</param>
        public void AddMessage(string msg)
        {
            if (String.IsNullOrEmpty(msg)) throw new ArgumentNullException(nameof(msg));

            lock (_Lock)
            {
                _Messages.Add(DateTime.UtcNow, msg);
            }
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        /// <param name="disposing">Disposing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {

                }

                _End = null;
                _Messages = null;
                _Metadata = null;
                _Disposed = true;
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

        private double TotalMsBetween(DateTime start, DateTime end)
        {
            try
            {
                start = start.ToUniversalTime();
                end = end.ToUniversalTime();
                TimeSpan total = end - start;
                return total.TotalMilliseconds;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}