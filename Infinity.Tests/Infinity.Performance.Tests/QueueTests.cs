using Infinity.Core;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Infinity.Tests.Performance
{
    public class QueueTests
    {
        ITestOutputHelper output;

        public QueueTests(ITestOutputHelper _output)
        {
            output = _output;
        }

        Queue<int> first = new Queue<int>();
        readonly object first_lock = new object();
        ConcurrentQueue<int> second = new ConcurrentQueue<int>();
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();

        [Fact]
        public void TestSpeed()
        {
            int res = 0;

            sw1.Start();

            lock(first_lock)
            {
                first.Enqueue(1);
                first.Dequeue();
            }

            sw1.Stop();

            sw2.Start();

            second.Enqueue(2);
            second.TryDequeue(out res);

            sw2.Stop();

            output.WriteLine(sw1.ElapsedTicks.ToString());
            output.WriteLine(sw2.ElapsedTicks.ToString());

            // pretty much the same speed
        }
    }
}
