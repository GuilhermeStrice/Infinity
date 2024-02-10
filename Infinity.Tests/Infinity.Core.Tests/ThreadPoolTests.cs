using Infinity.Core.Threading;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public class ThreadPoolTests
    {
        ITestOutputHelper output;
        ManualResetEvent mutex = new ManualResetEvent(false);
        long count = 0;

        public ThreadPoolTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        int desired_tests = 10000;

        [Fact]
        public void ThreadPoolTest()
        {
            OptimizedThreadPool.AdjustThreadCount(2);

            for (int i = 0; i < desired_tests; i++)
            {
                OptimizedThreadPool.EnqueueJob(Testt, null);
            }

            mutex.WaitOne(5000);

            output.WriteLine(count.ToString());
        }

        private void Testt(object? state)
        {
            Interlocked.Increment(ref count);

            if (Interlocked.Read(ref count) == desired_tests)
            {
                mutex.Set();
            }
        }

        [Fact]
        public void NetThreadPoolTest()
        {
            ThreadPool.SetMinThreads(2, 2);
            ThreadPool.SetMaxThreads(2, 2);

            Parallel.For(0, 100, (a) =>
            {
                OptimizedThreadPool.EnqueueJob(Testt, null);
            });

            mutex.WaitOne(2500);

            output.WriteLine(count.ToString());
        }
    }
}