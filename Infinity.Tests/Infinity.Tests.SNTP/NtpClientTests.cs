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
        public void Query()
        {
            int retries = 3;

            try
            {
                if (retries > 0)
                {
                    new NtpClient().Query();
                }
            }
            catch
            {
                retries--;
            }

            if (retries <= 0)
            {
                throw new Exception("failed");
            }
        }

        [Fact]
        public async Task QueryAsync()
        {
            int retries = 3;

            try
            {
                if (retries > 0)
                {
                    await new NtpClient().QueryAsync();
                }
            }
            catch
            {
                retries--;
            }

            if (retries <= 0)
            {
                throw new Exception("failed");
            }
        }

        [Fact]
        public void Timeout()
        {
            var timeout = TimeSpan.FromMilliseconds(500);

            // Note: pick a host that *drops* packets. The test will fail if the host merely *rejects* packets.
            var client = new NtpClient(IPAddress.Parse("8.8.8.8"), timeout);

            var timer = Stopwatch.StartNew();

            try
            {
                client.Query();
                Assert.Fail("Shouldn't get here. Expecting timeout!");
            }
            catch (SocketException ex) when (ex.ErrorCode == 10060 || ex.ErrorCode == 10035 || ex.ErrorCode == 110)
            {
                // We expect a socket timeout error
            }

            timer.Stop();

            Assert.True(timer.Elapsed >= timeout, timer.Elapsed.ToString());
            Assert.True(timer.Elapsed < timeout + timeout + timeout, timer.Elapsed.ToString());
        }
    }
}