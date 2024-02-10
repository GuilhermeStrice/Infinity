using Infinity.Core.Exceptions;
using System.Collections.Concurrent;

namespace Infinity.Core.Threading
{
    public static class OptimizedThreadPool
    {
        // work item id -> thread id
        private static FastConcurrentDictionary<int, int> thread_job_link = new FastConcurrentDictionary<int, int>();
        private static BlockingCollection<OptimizedThreadPoolJob> job_queue = new BlockingCollection<OptimizedThreadPoolJob>();

        private static List<OptimizedThread> worker_threads = new List<OptimizedThread>();

        private static int thread_count = Environment.ProcessorCount / 4;
        private static bool initialized = false;
        private static bool continue_working = true;

        private static int job_id_counter = 0;
        
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
            lock (worker_threads)
            {
                thread_count = _thread_count;

                int current_size = worker_threads.Count;

                if (current_size > thread_count)
                {
                    for (int i = current_size - 1; i > thread_count; i--)
                    {
                        worker_threads[i].Thread.Interrupt();
                        worker_threads.RemoveAt(i);
                    }
                }
                else if (current_size < thread_count)
                {
                    int new_threads_count = thread_count - current_size;

                    for (int i = 0; i < new_threads_count; i++)
                    {
                        var worker_thread = new OptimizedThread(WorkAction, i);
                        worker_threads.Add(worker_thread);
                    }
                }
            }
        }

        public static int EnqueueJob(ThreadedAction _method_to_execute, object? _state = null)
        {
            Initialize();

            if (_method_to_execute == null)
            {
                throw new InfinityThreadPoolException("Method cannot be null");
            }

            int job_id = AllocateJobId();

            var job = new OptimizedThreadPoolJob();
            job.MethodToExecute = _method_to_execute;
            job.State = _state;
            job.Id = job_id;

            job_queue.Add(job);

            return job_id;
        }

        public static bool CancelJob(int _job_id)
        {
            lock (job_queue)
            {
                // first check if the job hasn't been executed
                for (int i = 0; i < job_queue.Count; i++)
                {
                    var item = job_queue.ElementAt(i);
                    if (item.Id == _job_id)
                    {
                        item.WasCancelled = true;
                        return true;
                    }
                }
            }

            // else its probably running
            if (!thread_job_link.TryRemove(_job_id, out var thread_id))
            {
                return false;
            }

            lock (worker_threads)
            {
                worker_threads[thread_id].Cancel();
            }

            return true;
        }

        private static void WorkAction(int _thread_id)
        {
            while (continue_working)
            {
                OptimizedThreadPoolJob job = null;
                bool success = false;
                lock (job_queue)
                {
                    success = job_queue.TryTake(out job, 200);
                }

                if (success)
                {
                    if (!job.WasCancelled)
                    {
                        job.MethodToExecute.Invoke(job.State);

                        thread_job_link.Remove(job.Id);
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
                            var worker_thread = new OptimizedThread(WorkAction, i);
                            worker_threads.Add(worker_thread);
                        }
                    }
                }

                initialized = true;
            }
        }

        private static int AllocateJobId()
        {
            if (job_id_counter == int.MaxValue)
            {
                job_id_counter = 0;
                return job_id_counter;
            }

            return ++job_id_counter;
        }
    }
}
