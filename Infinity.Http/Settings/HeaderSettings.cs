namespace Infinity.Http
{
    public class HeaderSettings
    {
        /// <summary>
        /// Automatically set content length if not already set.
        /// </summary>
        public bool IncludeContentLength { get; set; } = true;

        /// <summary>
        /// Headers to add to each request.
        /// </summary>
        public Dictionary<string, string> DefaultHeaders
        {
            get
            {
                return default_headers;
            }
            set
            {
                if (value == null)
                {
                    default_headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                }
                else
                {
                    default_headers = value;
                }
            }
        }

        private Dictionary<string, string> default_headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            { WebserverConstants.HeaderAccessControlAllowOrigin, "*" },
            { WebserverConstants.HeaderAccessControlAllowMethods, "OPTIONS, HEAD, GET, PUT, POST, DELETE, PATCH" },
            { WebserverConstants.HeaderAccessControlAllowHeaders, "*" },
            { WebserverConstants.HeaderAccessControlExposeHeaders, "" },
            { WebserverConstants.HeaderAccept, "*/*" },
            { WebserverConstants.HeaderAcceptLanguage, "en-US, en" },
            { WebserverConstants.HeaderAcceptCharset, "ISO-8859-1, utf-8" },
            { WebserverConstants.HeaderCacheControl, "no-cache" },
            { WebserverConstants.HeaderConnection, "close" },
            { WebserverConstants.HeaderHost, "localhost:8000" }
        };

        /// <summary>
        /// Headers that will be added to every response unless previously set.
        /// </summary>
        public HeaderSettings()
        {

        }
    }
}
