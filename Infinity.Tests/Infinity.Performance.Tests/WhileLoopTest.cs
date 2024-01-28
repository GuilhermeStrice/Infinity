using Infinity.Core.Threading;

namespace Infinity.Performance.Tests
{
    public class WhileLoopTest
    {
        //[Fact]
        public void LoopTest()
        {
            while (true)
            {
                // do nothing
                
            }
        }

        //[Fact]
        public void ForLoopTest()
        {
            for (;;)
            {
                // do nothing

            }
        }

        public void DoSomething()
        {
            int j = 1;

            for (int i = 0; i < 10000; i++)
            {
                j += i / 5;
            }
        }

        // doesn't use cpu
        [Fact]
        public void ThreadWorkerTest()
        {
            WorkerThread workerThread = new WorkerThread();

            ManualResetEvent mutex = new ManualResetEvent(false);

            mutex.WaitOne(10000);

            //workerThread.Work(DoSomething);

            mutex.WaitOne(10000);
            
            //workerThread.Work(DoSomething);
        }
    }
}
