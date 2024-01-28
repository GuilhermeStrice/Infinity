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
    }
}
