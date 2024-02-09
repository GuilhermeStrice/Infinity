using Infinity.Core;
using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Infinity.Performance.Tests
{
    public class DictionaryTest
    {
        ITestOutputHelper output;

        public DictionaryTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        Dictionary<int, int> first = new Dictionary<int, int>();
        readonly object first_lock = new object();
        ConcurrentDictionary<int, int> second = new ConcurrentDictionary<int, int>();
        FastConcurrentDictionary<int, int> third = new FastConcurrentDictionary<int, int>();
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();
        Stopwatch sw3 = new Stopwatch();

        [Fact]
        public void Locking_performance_test()
        {
            int val = 0;

            sw1.Start();

            Parallel.For(1, 10000, (n) =>
            {
                lock (first)
                {
                    first.TryAdd(n, n);
                    first.TryGetValue(9999, out val);
                }
            });

            sw1.Stop();

            output.WriteLine($"{val}");

            sw2.Start();

            Parallel.For(1, 10000, (n) =>
            {
                second.TryAdd(n, n);
                second.TryGetValue(9999, out val);
            });

            sw2.Stop();

            output.WriteLine($"{val}");

            sw3.Start();

            Parallel.For(1, 10000, (n) =>
            {
                third.TryAdd(n, n);
                third.TryGetValue(9999, out val);
            });

            sw3.Stop();

            output.WriteLine($"{val}");
            
            output.WriteLine("");

            output.WriteLine(sw1.ElapsedTicks.ToString());
            output.WriteLine(sw2.ElapsedTicks.ToString());
            output.WriteLine(sw3.ElapsedTicks.ToString());
            // well, dictionary with locking is faster than concurrent dictionary
            // when debugging FasterConcurrentDictionary is slower, but when run in release mode it's still faster than ConcurrentDictionary
        }
    }
}