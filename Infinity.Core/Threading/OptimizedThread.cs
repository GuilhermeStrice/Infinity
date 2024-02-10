namespace Infinity.Core.Threading
{
    internal class OptimizedThread
    {
        public Thread Thread;
        public int Id;

        private Action<int> loop;

        public OptimizedThread(Action<int> _loop, int _thread_id)
        {
            loop = _loop;
            Id = _thread_id;
            Thread = new Thread(Loop);
            Thread.Start();
        }

        public void Loop()
        {
            loop.Invoke(Id);
        }

        internal void Cancel()
        {
            // Safely terminate thread (ThreadInterruptException exception needs to be handled by the developer)
            Thread.Interrupt();
            Thread.Join();

            // Restart thread
            Thread.Start();
        }
    }
}
