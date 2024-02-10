using System.Diagnostics;
using System.Net;
using Xunit.Abstractions;

namespace Infinity.SNTP.Tests
{
    public class NtpClientTests
    {
        ITestOutputHelper output;

        public NtpClientTests(ITestOutputHelper _output)
        {
            output = _output;
        }

        [Fact]
        public void Timeout()
        {
            var mutex = new ManualResetEvent(false);

            var timeout = TimeSpan.FromMilliseconds(1000);

            // Note: pick a host that *drops* packets. The test will fail if the host merely *rejects* packets.
            var client = new NtpClient(IPAddress.Parse("192.168.0.0"), timeout);

            var timer = Stopwatch.StartNew();

            client.OnNtpReceived += (NtpClock obj) =>
            {
                throw new Exception("shouldn't happen");
            };

            client.OnInternalError += (Exception ex) =>
            {
                mutex.Set();
            };

            mutex.WaitOne(timeout);

            timer.Stop();

            Assert.True(timer.Elapsed >= timeout, timer.Elapsed.ToString());
            Assert.True(timer.Elapsed < timeout + timeout + timeout, timer.Elapsed.ToString());
        }

        [Fact]
        public void Query()
        {
            var mutex = new ManualResetEvent(false);
            var client = new NtpClient();

            client.OnNtpReceived += (NtpClock obj) =>
            {
                mutex.Set();
            };

            client.OnInternalError += (Exception ex) =>
            {
                output.WriteLine(ex.Message);
            };

            client.Query();

            mutex.WaitOne(5000);
        }

        //[Fact]
        public void QueryStressed()
        {
            int count = 0;

            var mutex = new ManualResetEvent(false);
            var client = new NtpClient();

            client.OnNtpReceived += (NtpClock obj) =>
            {
                count++;
                if (count == 50)
                    mutex.Set();
                else
                    client.Query();
            };

            client.OnInternalError += (Exception ex) =>
            {
                output.WriteLine(ex.Message);
            };

            client.Query();

            mutex.WaitOne(5000);
        }
    }
}