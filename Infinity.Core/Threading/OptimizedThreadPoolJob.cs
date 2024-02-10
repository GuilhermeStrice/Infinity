namespace Infinity.Core.Threading
{
    public delegate void ThreadedAction(object? state);

    public class OptimizedThreadPoolJob
    {
        public ThreadedAction? MethodToExecute;
        public object? State;
        public int Id = -1;
        public bool WasCancelled = false;
    }
}
