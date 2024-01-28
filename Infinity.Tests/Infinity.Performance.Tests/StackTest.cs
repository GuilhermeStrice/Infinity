using System.Collections.Concurrent;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Infinity.Performance.Tests
{
    public class StackTest
    {
        ITestOutputHelper output;

        public StackTest(ITestOutputHelper output)
        {
            this.output = output;
        }

        Stack<int> first = new Stack<int>();
        readonly object first_lock = new object();
        ConcurrentStack<int> second = new ConcurrentStack<int>();
        Stopwatch sw1 = new Stopwatch();
        Stopwatch sw2 = new Stopwatch();

        [Fact]
        public void Locking_performance_test()
        {
            int val = 0;

            sw1.Start();

            Parallel.For(1, 10000, (n) =>
            {
                lock (first)
                {
                    first.Push(n);
                    if (n == 9999)
                        val = first.Pop();
                }
            });

            sw1.Stop();

            output.WriteLine($"{val}");

            sw2.Start();

            Parallel.For(1, 10000, (n) =>
            {
                second.Push(n);
                if (n == 9999)
                    second.TryPop(out int val);
            });

            sw2.Stop();

            output.WriteLine($"{val}");
            
            output.WriteLine("");

            output.WriteLine(sw1.ElapsedTicks.ToString());
            output.WriteLine(sw2.ElapsedTicks.ToString());
            // ConcurrentStack is faster
        }
    }
}