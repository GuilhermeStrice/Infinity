using Infinity.Core.Exceptions;
using System.Collections.Concurrent;

namespace Infinity.Core.Threading
{
    public static class OptimizedThreadPool
    {
        private static BlockingCollection<OptimizedThreadPoolWorkItem> job_queue = new BlockingCollection<OptimizedThreadPoolWorkItem>();
        private static BlockingCollection<OptimizedThreadPoolWorkItem> callback_queue = new BlockingCollection<OptimizedThreadPoolWorkItem>();

        private static object threads_list_lock = new object();

        private static List<Thread> worker_threads = new List<Thread>();
        private static List<Thread> callback_threads = new List<Thread>();

        private static int thread_count = Environment.ProcessorCount / 4;
        private static bool initialized = false;
        private static bool continue_working = true;
        
        public static bool IsWorking
        {
            get
            {
                return continue_working;
            }

            set
            {
                continue_working = value;
            }
        }

        /// <summary>
        /// For each worker thread there's a callback thread
        /// </summary>
        /// <param name="_thread_count"></param>
        public static void AdjustThreadCount(int _thread_count)
        {
            lock (threads_list_lock)
            {
                thread_count = _thread_count;

                int current_size = worker_threads.Count;

                if (current_size > thread_count)
                {
                    for (int i = current_size - 1; i > thread_count; i--)
                    {
                        worker_threads[i].Interrupt();
                        worker_threads.RemoveAt(i);

                        callback_threads[i].Interrupt();
                        callback_threads.RemoveAt(i);
                    }
                }
                else if (current_size < thread_count)
                {
                    int new_threads_count = thread_count - current_size;

                    for (int i = 0; i < new_threads_count; i++)
                    {
                        var worker_thread = new Thread(WorkAction);
                        worker_thread.Start();

                        worker_threads.Add(worker_thread);

                        var callback_thread = new Thread(CallbackAction);
                        callback_thread.Start();

                        callback_threads.Add(callback_thread);
                    }
                }
            }
        }

        public static void EnqueueJob(ThreadedAction _method_to_execute, ThreadedAction? _callback, object? _state = null)
        {
            Initialize();

            if (_method_to_execute == null)
            {
                throw new InfinityThreadPoolException("Method cannot be null");
            }
            
            var work_item = new OptimizedThreadPoolWorkItem();
            work_item.MethodToExecute = _method_to_execute;
            work_item.Callback = _callback;
            work_item.State = _state;

            job_queue.Add(work_item);
        }

        private static void WorkAction()
        {
            while (continue_working)
            {
                lock (job_queue)
                {
                    if (job_queue.TryTake(out var work_item, 50))
                    {
                        work_item.MethodToExecute.Invoke(work_item.State);
                        if (work_item.Callback != null)
                        {
                            callback_queue.Add(work_item);
                        }
                    }
                }
            }
        }

        private static void CallbackAction()
        {
            while (continue_working)
            {
                lock (callback_queue)
                {
                    if (callback_queue.TryTake(out var work_item, 50))
                    {
                        work_item.Callback.Invoke(work_item.State);
                    }
                }
            }
        }

        private static void Initialize()
        {
            if (!initialized)
            {
                lock (worker_threads)
                {
                    if (worker_threads.Count == 0)
                    {
                        for (int i = 0; i < thread_count; i++)
                        {
                            var worker_thread = new Thread(WorkAction);
                            worker_thread.Start();

                            worker_threads.Add(worker_thread);

                            var callback_thread = new Thread(CallbackAction);
                            callback_thread.Start();

                            callback_threads.Add(callback_thread);
                        }
                    }
                }

                initialized = true;
            }
        }
    }
}
