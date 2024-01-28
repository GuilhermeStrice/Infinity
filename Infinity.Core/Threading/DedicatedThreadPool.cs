using System.Collections.Concurrent;

namespace Infinity.Core.Threading
{
    public delegate void ThreadedCallback(object? state);

    public static class DedicatedThreadPool
    {
        private static ConcurrentQueue<ThreadedCallback> job_queue = new ConcurrentQueue<ThreadedCallback>();
        private static List<Thread> worker_threads = new List<Thread>();

        private static int thread_count = Environment.ProcessorCount / 4;

        public static void AdjustThreadCount(int _thread_count)
        {
            lock (worker_threads)
            {
                thread_count = _thread_count;

                int current_size = worker_threads.Count;

                if (current_size > thread_count)
                {
                    worker_threads.RemoveRange(current_size - 1, thread_count - current_size);
                }
                else
                {
                    int new_threads_count = thread_count - current_size;
                    var new_threads = new List<Thread>();

                    for (int i = 0; i < new_threads_count; i++)
                    {
                        new_threads.Add(new Thread(ProcessQueue));
                    }

                    worker_threads.AddRange(new_threads);
                }
            }
        }

        public static void EnqueueJob(ThreadedCallback tc, object? state)
        {
            job_queue.Enqueue(tc);
        }

        private static void ProcessQueue()
        {

        }
    }
}
