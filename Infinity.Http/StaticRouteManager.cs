namespace Infinity.Http
{
    /// <summary>
    /// Static route manager.  Static routes are used for requests using any HTTP method to a specific path.
    /// </summary>
    public class StaticRouteManager
    {
        private List<StaticRoute> routes = new List<StaticRoute>();
        private readonly object @lock = new object();

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public StaticRouteManager()
        {
        }

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path, i.e. /path/to/resource.</param>
        /// <param name="handler">Method to invoke.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public void Add(HttpMethod _method, string _path, Func<HttpContext, Task> _handler, Guid _guid = default, object _metadata = null)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            if (_handler == null)
            {
                throw new ArgumentNullException(nameof(_handler));
            }

            StaticRoute r = new StaticRoute(_method, _path, _handler, _guid, _metadata);
            Add(r);
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="path">URL path.</param>
        public void Remove(HttpMethod _method, string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            StaticRoute r = Get(_method, _path);
            if (r == null || r == default(StaticRoute))
            { 
                return;
            }
            else
            {
                lock (@lock)
                {
                    routes.Remove(r);
                }
                 
                return;
            }
        }

        /// <summary>
        /// Retrieve a static route.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path.</param>
        /// <returns>StaticRoute if the route exists, otherwise null.</returns>
        public StaticRoute Get(HttpMethod _method, string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }
            
            _path = _path.ToLower();
            if (!_path.StartsWith("/"))
            {
                _path = "/" + _path;
            }

            if (!_path.EndsWith("/"))
            {
                _path = _path + "/";
            }

            lock (@lock)
            {
                StaticRoute curr = routes.FirstOrDefault(i => i.Method == _method && i.Path == _path);
                if (curr == null || curr == default(StaticRoute))
                {
                    return null;
                }
                else
                {
                    return curr;
                }
            }
        }

        /// <summary>
        /// Check if a static route exists.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path.</param>
        /// <returns>True if exists.</returns>
        public bool Exists(HttpMethod _method, string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }
             
            _path = _path.ToLower();
            if (!_path.StartsWith("/"))
            {
                _path = "/" + _path;
            }

            if (!_path.EndsWith("/"))
            {
                _path = _path + "/";
            }

            lock (@lock)
            {
                StaticRoute curr = routes.FirstOrDefault(i => i.Method == _method && i.Path == _path);
                if (curr == null || curr == default(StaticRoute))
                { 
                    return false;
                }
            }
             
            return true;
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path.</param>
        /// <param name="_route">Matching route.</param>
        /// <returns>Method to invoke.</returns>
        public Func<HttpContext, Task> Match(HttpMethod _method, string _path, out StaticRoute _route)
        {
            _route = null;
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            _path = _path.ToLower();
            if (!_path.StartsWith("/"))
            {
                _path = "/" + _path;
            }

            if (!_path.EndsWith("/"))
            {
                _path = _path + "/";
            }

            lock (@lock)
            {
                StaticRoute curr = routes.FirstOrDefault(i => i.Method == _method && i.Path == _path);
                if (curr == null || curr == default(StaticRoute))
                {
                    return null;
                }
                else
                {
                    _route = curr;
                    return curr.Handler;
                }
            }
        }

        private void Add(StaticRoute _route)
        {
            if (_route == null)
            {
                throw new ArgumentNullException(nameof(_route));
            }
            
            _route.Path = _route.Path.ToLower();
            if (!_route.Path.StartsWith("/"))
            {
                _route.Path = "/" + _route.Path;
            }

            if (!_route.Path.EndsWith("/"))
            {
                _route.Path = _route.Path + "/";
            }

            if (Exists(_route.Method, _route.Path))
            { 
                return;
            }

            lock (@lock)
            {
                routes.Add(_route); 
            }
        }

        private void Remove(StaticRoute _route)
        {
            if (_route == null)
            {
                throw new ArgumentNullException(nameof(_route));
            }

            lock (@lock)
            {
                routes.Remove(_route);
            }
             
            return;
        }
    }
}
