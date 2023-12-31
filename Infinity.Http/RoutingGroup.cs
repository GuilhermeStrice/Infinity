namespace Infinity.Http
{
    /// <summary>
    /// Routing group.
    /// </summary>
    public class RoutingGroup
    {
        /// <summary>
        /// Static routes.
        /// </summary>
        public StaticRouteManager Static
        {
            get
            {
                return @static;
            }
            set
            {
                if (value == null)
                {
                    @static = new StaticRouteManager();
                }
                else
                {
                    @static = value;
                }
            }
        }

        /// <summary>
        /// Content routes.
        /// </summary>
        public ContentRouteManager Content
        {
            get
            {
                return content;
            }
            set
            {
                if (value == null)
                {
                    content = new ContentRouteManager();
                }
                else
                {
                    content = value;
                }
            }
        }

        /// <summary>
        /// Parameter routes.
        /// </summary>
        public ParameterRouteManager Parameter
        {
            get
            {
                return parameter;
            }
            set
            {
                if (value == null)
                {
                    parameter = new ParameterRouteManager();
                }
                else
                {
                    parameter = value;
                }
            }
        }

        private StaticRouteManager @static = new StaticRouteManager();
        private ContentRouteManager content = new ContentRouteManager();
        private ParameterRouteManager parameter = new ParameterRouteManager();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RoutingGroup()
        {
        }
    }
}
