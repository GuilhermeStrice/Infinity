using System.Collections.Specialized;
using Infinity.Core;

namespace Infinity.Http
{
    /// <summary>
    /// Parameter route manager.  Parameter routes are used for requests using any HTTP method to any path where parameters are defined in the URL.
    /// For example, /{version}/api.
    /// For a matching URL, the HttpRequest.Url.Parameters will contain a key called 'version' with the value found in the URL.
    /// </summary>
    public class ParameterRouteManager
    {
        /// <summary>
        /// Directly access the underlying URL matching library.
        /// This is helpful in case you want to specify the matching behavior should multiple matches exist.
        /// </summary>
        public UrlMatcher Matcher
        {
            get
            {
                return matcher;
            }
        }

        private UrlMatcher matcher = new UrlMatcher();
        private readonly object @lock = new object();
        private Dictionary<ParameterRoute, Func<HttpContext, Task>> routes = new Dictionary<ParameterRoute, Func<HttpContext, Task>>();

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public ParameterRouteManager()
        {
        }

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path, i.e. /path/to/resource.</param>
        /// <param name="_handler">Method to invoke.</param>
        /// <param name="_guid">Globally-unique identifier.</param>
        /// <param name="_metadata">User-supplied metadata.</param>
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

            lock (@lock)
            {
                ParameterRoute pr = new ParameterRoute(_method, _path, _handler, _guid, _metadata);
                routes.Add(pr, _handler);
            }
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path.</param>
        public void Remove(HttpMethod _method, string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            lock (@lock)
            {
                if (routes.Any(r => r.Key.Method == _method && r.Key.Path.Equals(_path)))
                {
                    List<ParameterRoute> removeList = routes.Where(r => r.Key.Method == _method && r.Key.Path.Equals(_path))
                        .Select(r => r.Key)
                        .ToList();

                    foreach (ParameterRoute remove in removeList)
                    {
                        routes.Remove(remove);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieve a parameter route.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path.</param>
        /// <returns>ParameterRoute if the route exists, otherwise null.</returns>
        public ParameterRoute Get(HttpMethod _method, string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            lock (@lock)
            {
                if (routes.Any(r => r.Key.Method == _method && r.Key.Path.Equals(_path)))
                {
                    return routes.First(r => r.Key.Method == _method && r.Key.Path.Equals(_path)).Key;
                }
            }

            return null;
        }

        /// <summary>
        /// Check if a content route exists.
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

            lock (@lock)
            {
                return routes.Any(r => r.Key.Method == _method && r.Key.Path.Equals(_path));
            }
        }

        /// <summary>
        /// Match a request method and URL to a handler method.
        /// </summary>
        /// <param name="_method">The HTTP method.</param>
        /// <param name="_path">URL path.</param>
        /// <param name="_vals">Values extracted from the URL.</param>
        /// <param name="_pr">Matching route.</param>
        /// <returns>True if match exists.</returns>
        public Func<HttpContext, Task> Match(HttpMethod _method, string _path, out NameValueCollection _vals, out ParameterRoute _pr)
        {
            _pr = null;
            _vals = null;
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            string consolidatedPath = BuildConsolidatedPath(_method, _path);

            lock (@lock)
            {
                foreach (KeyValuePair<ParameterRoute, Func<HttpContext, Task>> route in routes)
                {
                    if (matcher.Match(consolidatedPath, BuildConsolidatedPath(route.Key.Method, route.Key.Path), out _vals))
                    {
                        _pr = route.Key;
                        return route.Value;
                    }
                }
            }

            return null;
        }

        private string BuildConsolidatedPath(HttpMethod _method, string _path)
        {
            return _method.ToString() + " " + _path;
        }
    }
}
