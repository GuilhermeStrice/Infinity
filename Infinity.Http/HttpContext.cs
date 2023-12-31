using Infinity.Core;
using System.Net;
using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// HTTP context including both request and response.
    /// </summary>
    public class HttpContext
    {
        /// <summary>
        /// UTC timestamp from when the context object was created.
        /// </summary>
        [JsonPropertyOrder(0)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// The HTTP request that was received.
        /// </summary>
        [JsonPropertyOrder(1)]
        public HttpRequest Request { get; set; } = null;

        /// <summary>
        /// Type of route.
        /// </summary>
        [JsonPropertyOrder(2)]
        public RouteTypeEnum RouteType { get; set; } = RouteTypeEnum.Default;

        /// <summary>
        /// Matched route.
        /// </summary>
        [JsonPropertyOrder(3)]
        public object Route { get; set; } = null;

        /// <summary>
        /// Globally-unique identifier for the context.
        /// </summary>
        [JsonPropertyOrder(4)]
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Cancellation token source.
        /// </summary>
        [JsonPropertyOrder(5)]
        [JsonIgnore]
        public CancellationTokenSource TokenSource
        {
            get
            {
                return token_source;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(TokenSource));
                }

                token_source = value;
            }
        }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        [JsonPropertyOrder(6)]
        [JsonIgnore]
        public CancellationToken Token { get; set; } = token_source.Token;

        /// <summary>
        /// The HTTP response that will be sent.  This object is preconstructed on your behalf and can be modified directly.
        /// </summary>
        [JsonPropertyOrder(998)]
        public HttpResponse Response { get; set; } = null;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        private static CancellationTokenSource token_source = new CancellationTokenSource();

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpContext()
        {

        }

        /// <summary>
        /// Instantiate
        /// </summary>
        /// <param name="ctx">HTTP listener context.</param>
        /// <param name="settings">Settings.</param>
        /// <param name="events">Events.</param>
        /// <param name="serializer">Serializer.</param>
        internal HttpContext(HttpListenerContext _ctx, WebserverSettings _settings, ISerializationHelper _serializer)
        {
            if (_ctx == null)
            {
                throw new ArgumentNullException(nameof(_ctx));
            }

            Request = new HttpRequest(_ctx, _serializer);
            Response = new HttpResponse(Request, _ctx, _settings, _serializer);
        }
    }
}