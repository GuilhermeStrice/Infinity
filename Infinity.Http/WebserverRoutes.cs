namespace Infinity.Http
{
    /// <summary>
    /// Route manager.
    /// </summary>
    public class WebserverRoutes
    {
        /// <summary>
        /// Method to invoke when an OPTIONS request is received.
        /// </summary>
        public Func<HttpContext, Task> Preflight { get; set; } = null;

        /// <summary>
        /// Method to invoke prior to routing.
        /// </summary>
        public Func<HttpContext, Task> PreRouting { get; set; } = null;

        /// <summary>
        /// Pre-authentication routes.
        /// </summary>
        public RoutingGroup PreAuthentication
        {
            get
            {
                return pre_authentication;
            }
            set
            {
                if (value == null)
                {
                    pre_authentication = new RoutingGroup();
                }
                else
                {
                    pre_authentication = value;
                }
            }
        }

        /// <summary>
        /// Method to invoke to authenticate a request.
        /// Attach any session-related metadata to the HttpContextBase.Metadata property.
        /// </summary>
        public Func<HttpContext, Task> AuthenticateRequest { get; set; } = null;

        /// <summary>
        /// Post-authentication routes.
        /// </summary>
        public RoutingGroup PostAuthentication
        {
            get
            {
                return post_authentication;
            }
            set
            {
                if (value == null)
                {
                    PostAuthentication = new RoutingGroup();
                }
                else
                {
                    post_authentication = value;
                }
            }
        }

        /// <summary>
        /// Default route, when no other routes are available.
        /// </summary>
        public Func<HttpContext, Task> Default
        {
            get
            {
                return @default;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Default));
                }

                @default = value;
            }
        }

        /// <summary>
        /// Method invoked after routing, primarily to emit logging and telemetry.
        /// </summary>
        public Func<HttpContext, Task> PostRouting { get; set; } = null;

        private WebserverSettings _Settings = null;
        private RoutingGroup pre_authentication = new RoutingGroup();
        private RoutingGroup post_authentication = new RoutingGroup();
        private Func<HttpContext, Task> @default = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public WebserverRoutes()
        {

        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="_settings">Settings.</param>
        /// <param name="_defaultRoute">Default route.</param>
        public WebserverRoutes(WebserverSettings _settings, Func<HttpContext, Task> _defaultRoute)
        {
            _Settings = _settings ?? throw new ArgumentNullException(nameof(_settings));
            Default = _defaultRoute ?? throw new ArgumentNullException(nameof(_defaultRoute));
        }
    }
}
