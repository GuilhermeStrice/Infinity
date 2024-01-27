using Infinity.SNTP;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Xunit.Abstractions;

namespace Infinity.Tests.SNTP
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

            var timeout = TimeSpan.FromMilliseconds(500);

            // Note: pick a host that *drops* packets. The test will fail if the host merely *rejects* packets.
            var client = new NtpClient(IPAddress.Parse("8.8.8.8"), timeout);

            var timer = Stopwatch.StartNew();

            client.OnNtpReceived += (NtpClock obj) =>
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

            client.Query();

            mutex.WaitOne();
        }
    }
}