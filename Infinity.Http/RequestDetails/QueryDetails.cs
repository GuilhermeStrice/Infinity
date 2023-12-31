using System.Collections.Specialized;

namespace Infinity.Http
{
    public class QueryDetails
    {
        /// <summary>
        /// Querystring, excluding the leading '?'.
        /// </summary>
        public string Querystring
        {
            get
            {
                if (full_url.Contains("?"))
                {
                    return full_url.Substring(full_url.IndexOf("?") + 1, (full_url.Length - full_url.IndexOf("?") - 1));
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Query elements.
        /// </summary>
        public NameValueCollection Elements
        {
            get
            {
                NameValueCollection ret = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                string qs = Querystring;
                if (!string.IsNullOrEmpty(qs))
                {
                    string[] queries = qs.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
                    if (queries.Length > 0)
                    {
                        for (int i = 0; i < queries.Length; i++)
                        {
                            string[] query_parts = queries[i].Split('=');
                            if (query_parts != null && query_parts.Length == 2)
                            {
                                ret.Add(query_parts[0], query_parts[1]);
                            }
                            else if (query_parts != null && query_parts.Length == 1)
                            {
                                ret.Add(query_parts[0], null);
                            }
                        }
                    }
                }

                return ret;
            }
        }

        /// <summary>
        /// Query details.
        /// </summary>
        public QueryDetails()
        {

        }

        /// <summary>
        /// Query details.
        /// </summary>
        /// <param name="_full_url">Full URL.</param>
        public QueryDetails(string _full_url)
        {
            if (string.IsNullOrEmpty(_full_url))
            {
                throw new ArgumentNullException(nameof(_full_url));
            }

            full_url = _full_url;
        }

        private string full_url = null;
    }
}
