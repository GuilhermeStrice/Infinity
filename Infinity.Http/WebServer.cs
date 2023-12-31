using System.Collections.Specialized;
using System.Net;
using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// HttpServerLite web server.
    /// </summary>
    public class Webserver : IDisposable
    {
        /// <summary>
        /// Webserver settings.
        /// </summary>
        public WebserverSettings Settings
        {
            get
            {
                return settings;
            }
            set
            {
                if (value == null)
                {
                    settings = new WebserverSettings();
                }
                else
                {
                    settings = value;
                }
            }
        }

        /// <summary>
        /// Webserver routes.
        /// </summary>
        public WebserverRoutes Routes
        {
            get
            {
                return routes;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Routes));
                }

                routes = value;
            }
        }

        /// <summary>
        /// Webserver statistics.
        /// </summary>
        public WebserverStatistics Statistics
        {
            get
            {
                return statistics;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Statistics));
                }

                statistics = value;
            }
        }

        /// <summary>
        /// Set specific actions/callbacks to use when events are raised.
        /// </summary>
        public WebserverEvents Events
        {
            get
            {
                return events;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Events));
                }

                events = value;
            }
        }

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WebserverPages DefaultPages
        {
            get
            {
                return default_pages;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(DefaultPages));
                }

                default_pages = value;
            }
        }

        /// <summary>
        /// JSON serialization helper.
        /// </summary>
        [JsonIgnore]
        public ISerializationHelper Serializer
        {
            get
            {
                return serializer;
            }
            set
            {
                serializer = value ?? throw new ArgumentNullException(nameof(Serializer));
            }
        }

        /// <summary>
        /// Indicates whether or not the server is listening.
        /// </summary>
        public bool IsListening
        {
            get
            {
                return (http_listener != null) ? http_listener.IsListening : false;
            }
        }

        /// <summary>
        /// Number of requests being serviced currently.
        /// </summary>
        public int RequestCount
        {
            get
            {
                return request_count;
            }
        }

        private readonly string _Header = "[Webserver] ";
        private HttpListener http_listener = new HttpListener();
        private int request_count = 0;

        private CancellationTokenSource token_source = new CancellationTokenSource();
        private CancellationToken token;
        private Task accept_connections = null;

        private WebserverEvents events = new WebserverEvents();
        private WebserverPages default_pages = new WebserverPages();
        private WebserverSettings settings = new WebserverSettings();
        private WebserverStatistics statistics = new WebserverStatistics();
        private WebserverRoutes routes = new WebserverRoutes();
        private ISerializationHelper serializer = new DefaultSerializationHelper();

        /// <summary>
        /// Creates a new instance of the Watson webserver.
        /// </summary>
        /// <param name="_hostname">Hostname or IP address on which to listen.</param>
        /// <param name="_port">TCP port on which to listen.</param>
        /// <param name="_ssl">Specify whether or not SSL should be used (HTTPS).</param>
        /// <param name="_default_route">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public Webserver(string _hostname, int _port, bool _ssl, Func<HttpContext, Task> _default_route)
        {
            if (string.IsNullOrEmpty(_hostname))
            {
                _hostname = "localhost";
            }

            if (_port < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(_port));
            }

            settings = new WebserverSettings(_hostname, _port, _ssl);
            routes = new WebserverRoutes(settings, _default_route);
        }

        /// <summary>
        /// Creates a new instance of the webserver.
        /// If you do not provide a settings object, default settings will be used, which will cause the webserver to listen on http://localhost:8000, and send events to the console.
        /// </summary>
        /// <param name="_settings">Webserver settings.</param>
        /// <param name="_default_route">Method used when a request is received and no matching routes are found.  Commonly used as the 404 handler when routes are used.</param>
        public Webserver(WebserverSettings _settings, Func<HttpContext, Task> _default_route)
        {
            if (_settings == null)
            {
                _settings = new WebserverSettings();
            }

            settings = _settings;
            routes = new WebserverRoutes(settings, _default_route);

            WebserverConstants.HeaderHost = _settings.Hostname + ":" + _settings.Port;
            Routes.Default = _default_route;

            _Header = "[Webserver " + Settings.Prefix + "] ";
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="_token">Cancellation token useful for canceling the server.</param>
        public void Start(CancellationToken _token = default)
        {
            if (http_listener != null && http_listener.IsListening)
            {
                throw new InvalidOperationException("WatsonWebserver is already listening.");
            }

            Statistics = new WebserverStatistics();

            token_source = CancellationTokenSource.CreateLinkedTokenSource(_token);
            token = _token;

            http_listener = new HttpListener();
            http_listener.Prefixes.Add(Settings.Prefix);
            http_listener.Start();

            accept_connections = Task.Run(() => AcceptConnections(token), token);

            Events.HandleServerStarted(this, EventArgs.Empty);
        }

        /// <summary>
        /// Start accepting new connections.
        /// </summary>
        /// <param name="token">Cancellation token useful for canceling the server.</param>
        /// <returns>Task.</returns>
        public Task StartAsync(CancellationToken _token = default)
        {
            if (http_listener != null && http_listener.IsListening)
            {
                throw new InvalidOperationException("WatsonWebserver is already listening.");
            }

            Statistics = new WebserverStatistics();

            token_source = CancellationTokenSource.CreateLinkedTokenSource(token);
            token = _token;

            http_listener = new HttpListener();
            http_listener.Prefixes.Add(Settings.Prefix);
            http_listener.Start();

            accept_connections = Task.Run(() => AcceptConnections(token), token);

            Events.HandleServerStarted(this, EventArgs.Empty);

            return accept_connections;
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public void Stop()
        {
            if (!http_listener.IsListening)
            {
                throw new InvalidOperationException("WatsonWebserver is already stopped.");
            }

            if (http_listener != null && http_listener.IsListening)
            {
                http_listener.Stop();
            }

            if (token_source != null && !token_source.IsCancellationRequested)
            {
                token_source.Cancel();
            }
        }

        /// <summary>
        /// Tear down the server and dispose of background workers.
        /// Do not use this object after disposal.
        /// </summary>
        protected virtual void Dispose(bool _disposing)
        {
            if (_disposing)
            {
                if (http_listener != null && http_listener.IsListening)
                {
                    Stop();

                    http_listener.Close();
                }

                Events.HandleServerDisposing(this, EventArgs.Empty);

                http_listener = null;
                Settings = null;
                token_source = null;
                accept_connections = null;
            }
        }

        private async Task AcceptConnections(CancellationToken _token)
        {
            try
            {
                #region Process-Requests

                while (http_listener.IsListening)
                {
                    if (request_count >= Settings.IO.MaxRequests)
                    {
                        await Task.Delay(100, _token).ConfigureAwait(false);
                        continue;
                    }

                    HttpListenerContext listenerCtx = await http_listener.GetContextAsync().ConfigureAwait(false);
                    listenerCtx.Response.KeepAlive = Settings.IO.EnableKeepAlive;

                    Interlocked.Increment(ref request_count);
                    HttpContext ctx = null;
                    Func<HttpContext, Task> handler = null;

                    Task unawaited = Task.Run(async () =>
                    {
                        try
                        {
                            #region Build-Context

                            Events.HandleConnectionReceived(this, new NewConnectionEventArgs(
                                listenerCtx.Request.RemoteEndPoint.Address.ToString(),
                                listenerCtx.Request.RemoteEndPoint.Port));

                            ctx = new HttpContext(listenerCtx, Settings, Serializer);

                            Events.HandleRequestReceived(this, new RequestEventArgs(ctx));

                            if (Settings.Debug.Requests)
                            {
                                Events.Logger?.Invoke(
                                    _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                            }

                            Statistics.IncrementRequestCounter(ctx.Request.Method);
                            Statistics.IncrementReceivedPayloadBytes(ctx.Request.ContentLength);

                            #endregion

                            #region Check-Access-Control

                            if (!Settings.AccessControl.Permit(ctx.Request.Source.IpAddress))
                            {
                                Events.HandleRequestDenied(this, new RequestEventArgs(ctx));

                                if (Settings.Debug.AccessControl)
                                {
                                    Events.Logger?.Invoke(_Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " denied due to access control");
                                }

                                listenerCtx.Response.StatusCode = 403;
                                listenerCtx.Response.Close();
                                return;
                            }

                            #endregion

                            #region Preflight-Handler

                            if (ctx.Request.Method == HttpMethod.OPTIONS)
                            {
                                if (Routes.Preflight != null)
                                {
                                    if (Settings.Debug.Routing)
                                    {
                                        Events.Logger?.Invoke(
                                            _Header + "preflight route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                    }

                                    await Routes.Preflight(ctx).ConfigureAwait(false);
                                    if (!ctx.Response.ResponseSent)
                                        throw new InvalidOperationException("Preflight route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                    return;
                                }
                            }

                            #endregion

                            #region Pre-Routing-Handler

                            if (Routes.PreRouting != null)
                            {
                                await Routes.PreRouting(ctx).ConfigureAwait(false);
                                if (ctx.Response.ResponseSent)
                                {
                                    if (Settings.Debug.Routing)
                                    {
                                        Events.Logger?.Invoke(
                                            _Header + "prerouting terminated connection for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                    }

                                    return;
                                }
                                else
                                {
                                    // allow the connection to continue
                                }
                            }

                            #endregion

                            #region Pre-Authentication

                            if (Routes.PreAuthentication != null)
                            {
                                #region Static-Routes

                                if (Routes.PreAuthentication.Static != null)
                                {
                                    handler = Routes.PreAuthentication.Static.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out StaticRoute sr);
                                    if (handler != null)
                                    {
                                        if (Settings.Debug.Routing)
                                        {
                                            Events.Logger?.Invoke(
                                                _Header + "pre-auth static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                        }

                                        ctx.RouteType = RouteTypeEnum.Static;
                                        ctx.Route = sr;
                                        await handler(ctx).ConfigureAwait(false);
                                        if (!ctx.Response.ResponseSent)
                                            throw new InvalidOperationException("Pre-authentication static route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                        return;
                                    }
                                }

                                #endregion

                                #region Content-Routes

                                if (Routes.PreAuthentication.Content != null &&
                                    (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD))
                                {
                                    if (Routes.PreAuthentication.Content.Match(ctx.Request.Url.RawWithoutQuery, out ContentRoute cr))
                                    {
                                        if (Settings.Debug.Routing)
                                        {
                                            Events.Logger?.Invoke(
                                                _Header + "pre-auth content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                        }

                                        ctx.RouteType = RouteTypeEnum.Content;
                                        ctx.Route = cr;
                                        await Routes.PreAuthentication.Content.Handler(ctx).ConfigureAwait(false);
                                        if (!ctx.Response.ResponseSent)
                                            throw new InvalidOperationException("Pre-authentication content route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                        return;
                                    }
                                }

                                #endregion

                                #region Parameter-Routes

                                if (Routes.PreAuthentication.Parameter != null)
                                {
                                    handler = Routes.PreAuthentication.Parameter.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out NameValueCollection parameters, out ParameterRoute pr);
                                    if (handler != null)
                                    {
                                        ctx.Request.Url.Parameters = parameters;

                                        if (Settings.Debug.Routing)
                                        {
                                            Events.Logger?.Invoke(
                                                _Header + "pre-auth parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                        }

                                        ctx.RouteType = RouteTypeEnum.Parameter;
                                        ctx.Route = pr;
                                        await handler(ctx).ConfigureAwait(false);
                                        if (!ctx.Response.ResponseSent)
                                            throw new InvalidOperationException("Pre-authentication parameter route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                        return;
                                    }
                                }

                                #endregion
                            }

                            #endregion

                            #region Authentication

                            if (Routes.AuthenticateRequest != null)
                            {
                                await Routes.AuthenticateRequest(ctx);
                                if (ctx.Response.ResponseSent)
                                {
                                    if (Settings.Debug.Routing)
                                    {
                                        Events.Logger?.Invoke(_Header + "response sent during authentication for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                            ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                                    }

                                    return;
                                }
                                else
                                {
                                    // allow the connection to continue
                                }
                            }

                            #endregion

                            #region Post-Authentication

                            if (Routes.PostAuthentication != null)
                            {
                                #region Static-Routes

                                if (Routes.PostAuthentication.Static != null)
                                {
                                    handler = Routes.PostAuthentication.Static.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out StaticRoute sr);
                                    if (handler != null)
                                    {
                                        if (Settings.Debug.Routing)
                                        {
                                            Events.Logger?.Invoke(
                                                _Header + "post-auth static route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                        }

                                        ctx.RouteType = RouteTypeEnum.Static;
                                        ctx.Route = sr;
                                        await handler(ctx).ConfigureAwait(false);
                                        if (!ctx.Response.ResponseSent)
                                            throw new InvalidOperationException("Post-authentication static route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                        return;
                                    }
                                }

                                #endregion

                                #region Content-Routes

                                if (Routes.PostAuthentication.Content != null &&
                                    (ctx.Request.Method == HttpMethod.GET || ctx.Request.Method == HttpMethod.HEAD))
                                {
                                    if (Routes.PostAuthentication.Content.Match(ctx.Request.Url.RawWithoutQuery, out ContentRoute cr))
                                    {
                                        if (Settings.Debug.Routing)
                                        {
                                            Events.Logger?.Invoke(
                                                _Header + "post-auth content route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                        }

                                        ctx.RouteType = RouteTypeEnum.Content;
                                        ctx.Route = cr;
                                        await Routes.PostAuthentication.Content.Handler(ctx).ConfigureAwait(false);
                                        if (!ctx.Response.ResponseSent)
                                            throw new InvalidOperationException("Post-authentication content route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                        return;
                                    }
                                }

                                #endregion

                                #region Parameter-Routes

                                if (Routes.PostAuthentication.Parameter != null)
                                {
                                    handler = Routes.PostAuthentication.Parameter.Match(ctx.Request.Method, ctx.Request.Url.RawWithoutQuery, out NameValueCollection parameters, out ParameterRoute pr);
                                    if (handler != null)
                                    {
                                        ctx.Request.Url.Parameters = parameters;

                                        if (Settings.Debug.Routing)
                                        {
                                            Events.Logger?.Invoke(
                                                _Header + "post-auth parameter route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                                ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery);
                                        }

                                        ctx.RouteType = RouteTypeEnum.Parameter;
                                        ctx.Route = pr;
                                        await handler(ctx).ConfigureAwait(false);
                                        if (!ctx.Response.ResponseSent)
                                            throw new InvalidOperationException("Post-authentication parameter route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                        return;
                                    }
                                }

                                #endregion
                            }

                            #endregion

                            #region Default-Route

                            if (Settings.Debug.Routing)
                            {
                                Events.Logger?.Invoke(
                                    _Header + "default route for " + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                    ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full);
                            }

                            if (Routes.Default != null)
                            {
                                ctx.RouteType = RouteTypeEnum.Default;
                                await Routes.Default(ctx).ConfigureAwait(false);
                                if (!ctx.Response.ResponseSent)
                                    throw new InvalidOperationException("Default route for " + ctx.Request.Method.ToString() + " " + ctx.Request.Url.RawWithoutQuery + " did not send a response to the HTTP request.");
                                return;
                            }
                            else
                            {
                                ctx.Response.StatusCode = 404;
                                ctx.Response.ContentType = DefaultPages.Pages[404].ContentType;
                                await ctx.Response.Send(DefaultPages.Pages[404].Content).ConfigureAwait(false);
                                return;
                            }

                            #endregion
                        }
                        catch (Exception eInner)
                        {
                            ctx.Response.StatusCode = 500;
                            ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
                            await ctx.Response.Send(DefaultPages.Pages[500].Content).ConfigureAwait(false);
                            Events.HandleExceptionEncountered(this, new ExceptionEventArgs(ctx, eInner));
                        }
                        finally
                        {
                            Interlocked.Decrement(ref request_count);

                            if (ctx != null)
                            {
                                if (!ctx.Response.ResponseSent)
                                {
                                    ctx.Response.StatusCode = 500;
                                    ctx.Response.ContentType = DefaultPages.Pages[500].ContentType;
                                    await ctx.Response.Send(DefaultPages.Pages[500].Content).ConfigureAwait(false);
                                }

                                ctx.Timestamp.End = DateTime.UtcNow;

                                Events.HandleResponseSent(this, new ResponseEventArgs(ctx, ctx.Timestamp.TotalMs.Value));

                                if (Settings.Debug.Responses)
                                {
                                    Events.Logger?.Invoke(
                                        _Header + ctx.Request.Source.IpAddress + ":" + ctx.Request.Source.Port + " " +
                                        ctx.Request.Method.ToString() + " " + ctx.Request.Url.Full + ": " +
                                        ctx.Response.StatusCode + " [" + ctx.Timestamp.TotalMs.Value + "ms]");
                                }

                                if (ctx.Response.ContentLength > 0) Statistics.IncrementSentPayloadBytes(Convert.ToInt64(ctx.Response.ContentLength));
                                Routes.PostRouting?.Invoke(ctx).ConfigureAwait(false);
                            }
                        }

                    }, _token);
                }

                #endregion
            }
            catch (Exception e)
            {
                Events.HandleExceptionEncountered(this, new ExceptionEventArgs(null, e));
            }
            finally
            {
                Events.HandleServerStopped(this, EventArgs.Empty);
            }
        }
    }
}