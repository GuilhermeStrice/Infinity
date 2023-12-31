using System.Collections.Specialized;
using System.Net;

namespace Infinity.Http
{
    public class UrlDetails
    {
        /// <summary>
        /// Full URL.
        /// </summary>
        public string Full { get; set; } = null;

        /// <summary>
        /// Raw URL with query.
        /// </summary>
        public string RawWithQuery { get; set; } = null;

        /// <summary>
        /// Raw URL without query.
        /// </summary>
        public string RawWithoutQuery
        {
            get
            {
                if (!string.IsNullOrEmpty(RawWithQuery))
                {
                    if (RawWithQuery.Contains("?"))
                    {
                        return RawWithQuery.Substring(0, RawWithQuery.IndexOf("?"));
                    }
                    else
                    {
                        return RawWithQuery;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Raw URL elements.
        /// </summary>
        public string[] Elements
        {
            get
            {
                string raw_url = RawWithoutQuery;

                if (!string.IsNullOrEmpty(raw_url))
                {
                    while (raw_url.Contains("//"))
                    {
                        raw_url = raw_url.Replace("//", "/");
                    }

                    while (raw_url.StartsWith("/"))
                    {
                        raw_url = raw_url.Substring(1);
                    }

                    while (raw_url.EndsWith("/"))
                    {
                        raw_url = raw_url.Substring(0, raw_url.Length - 1);
                    }

                    string[] encoded = raw_url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (encoded != null && encoded.Length > 0)
                    {
                        string[] decoded = new string[encoded.Length];
                        for (int i = 0; i < encoded.Length; i++)
                        {
                            decoded[i] = WebUtility.UrlDecode(encoded[i]);
                        }

                        return decoded;
                    }
                }

                string[] ret = new string[0];
                return ret;
            }
        }

        /// <summary>
        /// Parameters found within the URL, if using parameter routes.
        /// </summary>
        public NameValueCollection Parameters
        {
            get
            {
                return parameters;
            }
            set
            {
                if (value == null)
                {
                    parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                }
                else
                {
                    parameters = value;
                }
            }
        }

        /// <summary>
        /// URL details.
        /// </summary>
        public UrlDetails()
        {
        }

        /// <summary>
        /// URL details.
        /// </summary>
        /// <param name="_full_url">Full URL.</param>
        /// <param name="_raw_url">Raw URL.</param>
        public UrlDetails(string _full_url, string _raw_url)
        {
            if (string.IsNullOrEmpty(_raw_url))
            {
                throw new ArgumentNullException(nameof(_raw_url));
            }

            Full = _full_url;
            RawWithQuery = _raw_url;
        }

        private NameValueCollection parameters = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
    }
}
