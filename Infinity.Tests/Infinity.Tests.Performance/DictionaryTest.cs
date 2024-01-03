using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Infinity.Tests.Performance
{
    public class DictionaryTest
    {
        ITestOutputHelper output;

        public DictionaryTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        Dictionary<int, int> first = new Dictionary<int, int>();
        ConcurrentDictionary<int, int> second = new ConcurrentDictionary<int, int>();
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();

        [Fact]
        public void Locking_performance_test()
        {
            ManualResetEvent re1 = new ManualResetEvent(false);
            sw1.Start();

            Parallel.For(1, 10000, (n) =>
            {
                lock (first)
                {
                    first.Add(n, n);
                }

                if (n == 999)
                {
                    sw1.Stop();
                    re1.Set();
                }
            });

            ManualResetEvent re2 = new ManualResetEvent(false);
            sw2.Start();

            Parallel.For(1, 10000, (n) =>
            {
                second.TryAdd(n, n);

                if (n == 999)
                {
                    sw2.Stop();
                    re2.Set();
                }
            });

            re1.WaitOne();
            re2.WaitOne();

            output.WriteLine(sw1.ElapsedTicks.ToString());
            output.WriteLine(sw2.ElapsedTicks.ToString());

            // well, dictionary with locking is faster than concurrent dictionary
        }
    }
}