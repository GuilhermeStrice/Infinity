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
            OptimizedThreadPool.AdjustThreadCount(1);

            for (int i = 0; i < 10; i++)
            {
                OptimizedThreadPool.EnqueueJob(Testt, null, null);
            }

            mutex.WaitOne(5000);

            output.WriteLine(count.ToString());
        }

        public void Testt(object? state)
        {
            count++;
            
            if (count == 9)
            {
                mutex.Set();
            }
        }
    }
}