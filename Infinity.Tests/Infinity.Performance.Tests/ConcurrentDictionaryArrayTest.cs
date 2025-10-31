using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Collections.Concurrent;

namespace Infinity.Performance.Tests
{
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ConcurrentDictionaryArrayTest
    {
        [GlobalSetup]
        public void Setup()
        {

        }

        [Benchmark]
        public void ConcurrentDictionaryTest()
        {
            ConcurrentDictionary<ushort, bool> test_dict = new ConcurrentDictionary<ushort, bool>();

            Parallel.For(0, ushort.MaxValue, (i) =>
            {
                test_dict.TryAdd((ushort)i, true);
            });

            Parallel.For(0, ushort.MaxValue, (i) =>
            {
                test_dict.TryRemove((ushort)i, out var _);
            });
        }

        [Benchmark]
        public void ArrayTest()
        {
            bool[] test_array = new bool[ushort.MaxValue];

            Parallel.For(0, ushort.MaxValue, (i) =>
            {
                lock (test_array)
                {
                    test_array[i] = true;
                }
            });

            Parallel.For(0, ushort.MaxValue, (i) =>
            {
                lock (test_array)
                {
                    test_array[i] = false;
                }
            });
        }
    }
}
