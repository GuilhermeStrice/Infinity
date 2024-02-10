namespace Infinity.Core.Threading
{
    public delegate void ThreadedAction(object? state);

    public class OptimizedThreadPoolWorkItem
    {
        public ThreadedAction? MethodToExecute;
        public object? State;
    }
}
