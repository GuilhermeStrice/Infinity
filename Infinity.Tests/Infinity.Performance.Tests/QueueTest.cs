using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Collections.Concurrent;

namespace Infinity.Performance.Tests
{
    [SimpleJob(RuntimeMoniker.Net80)]
    public class QueueTest
    {
        [GlobalSetup]
        public void Setup()
        {

        }

        [Benchmark]
        public void SimpleQueueTest()
        {
            Queue<int> first = new Queue<int>();

            Parallel.For(0, 50, (i) =>
            {
                lock (first)
                {
                    first.Enqueue(i);
                }

                lock (first)
                {
                    first.Dequeue();
                }
            });
        }

        [Benchmark]
        public void ConcurrentQueueTest()
        {
            ConcurrentQueue<int> second = new ConcurrentQueue<int>();

            Parallel.For(0, 50, (i) =>
            {
                second.Enqueue(i);
                second.TryDequeue(out var _);
            });
        }
    }
}
