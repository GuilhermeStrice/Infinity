using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Collections.Concurrent;

namespace Infinity.Performance.Tests
{
    [SimpleJob(RuntimeMoniker.Net80)]
    public class DictionaryTest
    {
        [GlobalSetup]
        public void Setup()
        {

        }

        [Benchmark]
        public void SimpleDictionaryTest()
        {
            Dictionary<int, int> first = new Dictionary<int, int>();

            Parallel.For(0, 50, (i) =>
            {
                lock (first)
                {
                    first.TryAdd(i, i);
                }

                lock (first)
                {
                    first.TryGetValue(i, out var _);
                }
            });
        }

        [Benchmark]
        public void ConcurrentDictionaryTest()
        {
            ConcurrentDictionary<int, int> second = new ConcurrentDictionary<int, int>();

            Parallel.For(0, 50, (i) =>
            {
                second.TryAdd(i, i);
                second.TryGetValue(i, out var _);
            });
        }
    }
}