using Infinity.Http;
using RestWrapper;
using Xunit.Abstractions;

namespace Infinity.Tests.Http
{
    public class HeadResponse
    {
        ITestOutputHelper output;

        public HeadResponse(ITestOutputHelper output)
        {
            this.output = output;
        }

        string _Hostname = "localhost";
        int _Port = 8080;
        WebserverSettings _Settings = null;
        Webserver _Server = null;
        string _Data = "Hello, world!";
        int _Counter = 0;
        int _Iterations = 10;

        [Fact]
        async Task HeadResponse_Test()
        {
            _Settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };

            using (_Server = new Webserver(_Settings, DefaultRoute))
            {
                output.WriteLine("Listening on " + _Settings.Prefix);
                _Server.Start();

                for (_Counter = 0; _Counter < _Iterations; _Counter++)
                {
                    await SendHeadRequest();
                }
            }
        }

        async Task DefaultRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;

            if (_Counter % 2 == 0)
            {
                output.WriteLine("Responding using ctx.Response.Send");
                await Task.Delay(250);
                ctx.Response.ContentLength = _Data.Length;
                await ctx.Response.Send();
            }
            else
            {
                output.WriteLine("Responding using ctx.Response.Send(len)");
                await Task.Delay(250);
                ctx.Response.ContentLength = _Data.Length;
                await ctx.Response.Send(_Data.Length);
            }

            return;
        }

        async Task SendHeadRequest()
        {
            using (RestRequest req = new RestRequest(_Settings.Prefix, System.Net.Http.HttpMethod.Head))
            {
                output.WriteLine("Sending REST request");

                using (RestResponse resp = await req.SendAsync())
                {
                    output.WriteLine(resp.ToString());
                }
            }
        }
    }
}
