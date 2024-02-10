using Infinity.Core.Threading;
using Xunit.Abstractions;

namespace Infinity.Core.Tests
{
    public class ThreadPoolTests
    {
        ITestOutputHelper output;
        ManualResetEvent mutex = new ManualResetEvent(false);
        int count = 0;

        public ThreadPoolTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ThreadPoolTest()
        {
            OptimizedThreadPool.AdjustThreadCount(2);

            Parallel.For(0, 100, (a) =>
            {
                OptimizedThreadPool.EnqueueJob(Testt, null);
            });

            mutex.WaitOne(2500);

            output.WriteLine(count.ToString());
        }

        private void Testt(object? state)
        {
            count++;

            if (count == 100)
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