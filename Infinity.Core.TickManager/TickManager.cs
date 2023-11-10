namespace Infinity.Core.TickManager
{
    public delegate void Tick();

    /// <summary>
    /// Handles ticking for an application
    /// </summary>
    public class TickManager
    {
        /// <summary>
        /// The frequency the application should be running at
        /// </summary>
        internal double internalFrequency;
        
        /// <summary>
        /// Milliseconds it should take to tick
        /// </summary>
        public double ShouldMSPT { get; private set; }

        /// <summary>
        /// Tick clock object
        /// </summary>
        internal TickClock tc = new TickClock();

        /// <summary>
        /// Event that gets called every time it ticks
        /// </summary>
        public event Tick OnTick;

        private object locker = new object();

        /// <summary>
        /// Used to run in a loop. Invokes the OnTick event
        /// </summary>
        /// /// <returns>Whether it ticked or not</returns>
        public bool Tick()
        {
            lock (locker)
            {
                if (CanTick())
                {
                    OnTick?.Invoke();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Gets the time remaining between the milliseconds it should take to process a tick and the real time it took
        /// </summary>
        /// <returns>Remaining time</returns>
        public double TimeRemaining()
        {
            return ShouldMSPT - MSPT;
        }

        /// <summary>
        /// The frequency your application will tick
        /// </summary>
        public int Frequency
        {
            get => (int)internalFrequency;
            set
            {
                // Tries to get an exact aproximation to the integer because Math...
                internalFrequency = value + 0.001;
                ShouldMSPT = TickHelper.TpsToMspt(internalFrequency);
            }
        }

        /// <summary>
        /// Milliseconds per tick that took to run last tick
        /// </summary>
        public double MSPT { get; private set; }

        /// <summary>
        /// The ticks per second the server is running at relative to last tick
        /// </summary>
        public double TPS 
        { 
            get => TickHelper.MsptToTps(MSPT);
        }

        /// <summary>
        /// Checks if the application can process the same tick relative to the frequency set
        /// </summary>
        /// <returns>Whether the application should process the next tick or not</returns>
        internal bool CanTick()
        {
            double tickTime = tc.Tick();
            bool canTick = tickTime > ShouldMSPT;
            if (canTick)
            {
                MSPT = tickTime;
                tc.Reset();
            }
            return canTick;
        }
    }
}
