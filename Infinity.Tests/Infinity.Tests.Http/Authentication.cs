using Infinity.Http;
using RestWrapper;
using Xunit.Abstractions;

namespace Infinity.Tests.Http
{
    public class Authentication
    {
        ITestOutputHelper output;

        public Authentication(ITestOutputHelper output)
        {
            this.output = output;
        }

        string _Hostname = "localhost";
        int _Port = 8080;
        WebserverSettings _Settings = null;
        Webserver _Server = null;
        int _Counter = 0;
        int _Iterations = 10;

        [Fact]
        async Task Authentication_Test()
        {
            _Settings = new WebserverSettings
            {
                Hostname = _Hostname,
                Port = _Port
            };

            using (_Server = new Webserver(_Settings, DefaultRoute))
            {
                _Server.Routes.AuthenticateRequest = AuthenticateRequest;
                _Server.Events.ExceptionEncountered += ExceptionEncountered;
                _Server.Events.ServerStopped += ServerStopped;
                _Server.Events.Logger = Console.WriteLine;

                output.WriteLine("Starting server on: " + _Settings.Prefix);

                _Server.Start();

                for (int i = 0; i < _Iterations; i++)
                {
                    using (RestRequest req = new RestRequest(_Settings.Prefix))
                    {
                        using (RestResponse resp = await req.SendAsync())
                        {
                            output.WriteLine(resp.StatusCode + ": " + resp.DataAsString);
                        }
                    }
                }
            }
        }

        async Task AuthenticateRequest(HttpContext ctx)
        {
            if (_Counter % 2 == 0)
            {
                // do nothing, permit
            }
            else
            {
                ctx.Response.StatusCode = 401;
                await ctx.Response.Send("Denied");
            }

            _Counter++;
        }

        void ExceptionEncountered(object sender, ExceptionEventArgs args)
        {
            _Server.Events.Logger(args.Exception.ToString());
        }

        void ServerStopped(object sender, EventArgs args)
        {
            _Server.Events.Logger("*** Server stopped");
        }

        async Task DefaultRoute(HttpContext ctx)
        {
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/plain";
            await ctx.Response.Send("Permitted");
            return;
        }
    }
}
