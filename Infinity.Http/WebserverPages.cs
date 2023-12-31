namespace Infinity.Http
{
    /// <summary>
    /// Default pages served.
    /// </summary>
    public class WebserverPages
    {
        /// <summary>
        /// Pages by status code.
        /// </summary>
        public Dictionary<int, Page> Pages
        {
            get
            {
                return pages;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Pages));
                }

                pages = value;
            }
        }

        private Dictionary<int, Page> pages = new Dictionary<int, Page>
        {
            { 400, new Page(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent400) },
            { 404, new Page(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent404) },
            { 500, new Page(WebserverConstants.ContentTypeHtml, WebserverConstants.PageContent500) }
        };

        /// <summary>
        /// Default pages served by Watson webserver.
        /// </summary>
        public WebserverPages()
        {
        }

        /// <summary>
        /// Page served by Watson webserver.
        /// </summary>
        public class Page
        {
            /// <summary>
            /// Content type.
            /// </summary>
            public string ContentType { get; private set; } = null;

            /// <summary>
            /// Content.
            /// </summary>
            public string Content { get; private set; } = null;

            /// <summary>
            /// Page served by Watson webserver.
            /// </summary>
            /// <param name="_content_type">Content type.</param>
            /// <param name="_content">Content.</param>
            public Page(string _content_type, string _content)
            {
                if (string.IsNullOrEmpty(_content_type))
                {
                    throw new ArgumentNullException(nameof(_content_type));
                }

                if (string.IsNullOrEmpty(_content))
                {
                    throw new ArgumentNullException(nameof(_content));
                }

                ContentType = _content_type;
                Content = _content;
            }
        }
    }
}
