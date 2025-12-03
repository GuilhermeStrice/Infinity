using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Infinity.Performance.Tests
{
    [SimpleJob(RuntimeMoniker.Net80)]
    public class ArrayBufferCopyTest
    {
        [GlobalSetup]
        public void Setup()
        {

        }

        [Benchmark]
        public void BufferBlockCopyTest()
        {
            var test_buffer = new byte[1024];

            Random random = new Random();
            random.NextBytes(test_buffer);

            List<byte[]> buffers = new List<byte[]>();

            for (int i = 0; i < 1000; i++)
            {
                var copy_buffer = new byte[test_buffer.Length];
                Buffer.BlockCopy(test_buffer, 0, copy_buffer, 0, test_buffer.Length);
                buffers.Add(copy_buffer);
            }
        }

        [Benchmark]
        public void ArrayCopyTest()
        {
            var test_buffer = new byte[1024];

            Random random = new Random();
            random.NextBytes(test_buffer);

            List<byte[]> buffers = new List<byte[]>();

            for (int i = 0; i < 1000; i++)
            {
                var copy_buffer = new byte[test_buffer.Length];
                Array.Copy(test_buffer, 0, copy_buffer, 0, test_buffer.Length);
                buffers.Add(copy_buffer);
            }
        }
    }
}
