using System.Collections.Specialized;

namespace Infinity.Http
{
    /// <summary>
    /// Exception event arguments.
    /// </summary>
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// IP address.
        /// </summary>
        public string Ip { get; set; } = null;

        /// <summary>
        /// Port number.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// URL.
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// Request query.
        /// </summary>
        public NameValueCollection Query { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Request headers.
        /// </summary>
        public NameValueCollection RequestHeaders { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Content length.
        /// </summary>
        public long RequestContentLength { get; set; } = 0;

        /// <summary>
        /// Response status.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Response headers.
        /// </summary>
        public NameValueCollection ResponseHeaders { get; set; } = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Response content length.
        /// </summary>
        public long? ResponseContentLength { get; set; } = 0;

        /// <summary>
        /// Exception.
        /// </summary>
        public Exception Exception { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="_ctx">Context.</param>
        /// <param name="e">Exception.</param>
        public ExceptionEventArgs(HttpContext _ctx, Exception _e)
        {
            if (_ctx != null)
            {
                Ip = _ctx.Request.Source.IpAddress;
                Port = _ctx.Request.Source.Port;
                Method = _ctx.Request.Method;
                Url = _ctx.Request.Url.Full;
                Query = _ctx.Request.Query.Elements;
                RequestHeaders = _ctx.Request.Headers;
                RequestContentLength = _ctx.Request.ContentLength;
                StatusCode = _ctx.Response.StatusCode;
                ResponseContentLength = _ctx.Response.ContentLength;
            }

            Exception = _e;
        }
    }
}
