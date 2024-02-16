using BenchmarkDotNet.Running;

namespace Infinity.Performance.Tests
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkRunner.Run<DictionaryTest>();
            BenchmarkRunner.Run<QueueTest>();
            BenchmarkRunner.Run<StackTest>();
        }
    }
}
