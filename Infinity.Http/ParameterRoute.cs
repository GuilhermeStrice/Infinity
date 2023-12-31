using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path containing parameters.
    /// </summary>
    public class ParameterRoute
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
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        [JsonPropertyOrder(1)]
        public string Path { get; set; } = null;

        /// <summary>
        /// The handler for the parameter route.
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
        /// <param name="_method">The HTTP method, i.e. GET, PUT, POST, DELETE, etc.</param>
        /// <param name="_path">The pattern against which the raw URL should be matched.</param>
        /// <param name="_handler">The method that should be called to handle the request.</param>
        /// <param name="_guid">Globally-unique identifier.</param>
        /// <param name="_metadata">User-supplied metadata.</param>
        public ParameterRoute(HttpMethod _method, string _path, Func<HttpContext, Task> _handler, Guid _guid = default, object _metadata = null)
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
            Path = _path;
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
