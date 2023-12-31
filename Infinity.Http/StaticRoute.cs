using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path.
    /// </summary>
    public class StaticRoute
    {
        /// <summary>
        /// Globally-unique identifier.
        /// </summary>
        [JsonPropertyOrder(-1)]
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The HTTP method, i.e. GET, PUT, POST, DELETE, etc.
        /// </summary>
        [JsonPropertyOrder(0)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Path { get; set; } = null;

        /// <summary>
        /// The handler for the static route.
        /// </summary>
        [JsonIgnore]
        public Func<HttpContext, Task> Handler { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        /// <summary>
        /// Create a new route object.
        /// </summary>
        /// <param name="method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="path">The raw URL, i.e. /foo/bar/.  Be sure this begins and ends with '/'.</param>
        /// <param name="handler">The method that should be called to handle the request.</param>
        /// <param name="guid">Globally-unique identifier.</param>
        /// <param name="metadata">User-supplied metadata.</param>
        public StaticRoute(HttpMethod _method, string _path, Func<HttpContext, Task> _handler, Guid _guid = default, object _metadata = null)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            if (_handler == null)
            {
                throw new ArgumentNullException(nameof(_handler));
            }

            Method = _method;
            
            Path = _path.ToLower();
            if (!Path.StartsWith("/"))
            {
                Path = "/" + Path;
            }

            if (!Path.EndsWith("/"))
            {
                Path = Path + "/";
            }

            Handler = _handler;

            if (_guid == default)
            {
                GUID = Guid.NewGuid();
            }
            else
            {
                GUID = _guid;
            }

            if (_metadata != null)
            {
                Metadata = _metadata;
            }
        }
    }
}
