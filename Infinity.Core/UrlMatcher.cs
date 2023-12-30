using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Infinity.Core
{
    /// <summary>
    /// URL matcher.
    /// </summary>
    public class UrlMatcher
    {
        // To do
        // Support values before or after the pattern

        private string header = "[UrlParser] ";

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public UrlMatcher()
        {

        }

        /// <summary>
        /// Match a URL against a pattern.
        /// For example, match URL /v1.0/something/else/32 against pattern /{v}/something/else/{id}.
        /// If a match exists, vals will contain keys name 'v' and 'id', and the associated values from the supplied URL.
        /// </summary>
        /// <param name="_url">The URL to evaluate.</param>
        /// <param name="_pattern">The pattern used to evaluate the URL.</param>
        /// <param name="_vals">Dictionary containing keys and values.</param>
        /// <returns>True if matched.</returns>
        public bool Match(string _url, string _pattern, out NameValueCollection _vals)
        {
            if (string.IsNullOrEmpty(_url))
            {
                throw new ArgumentNullException(nameof(_url));
            }

            if (string.IsNullOrEmpty(_pattern))
            {
                throw new ArgumentNullException(nameof(_pattern));
            }

            _vals = new NameValueCollection();
            string[] urlParts = _url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string[] patternParts = _pattern.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (urlParts.Length != patternParts.Length) return false;

            for (int i = 0; i < urlParts.Length; i++)
            {
                string paramName = ExtractParameter(patternParts[i]);

                if (string.IsNullOrEmpty(paramName))
                {
                    // no pattern
                    if (!urlParts[i].Equals(patternParts[i]))
                    {
                        Logger?.Invoke(header + "content mismatch at position " + i);
                        _vals = null;
                        return false;
                    }
                }
                else
                {
                    Logger?.Invoke(header + paramName.Replace("{", "").Replace("}", "") + ": " + urlParts[i]);
                    _vals.Add(
                        paramName.Replace("{", "").Replace("}", ""),
                        urlParts[i]);
                }
            }

            Logger?.Invoke(header + "match detected, " + _vals.Count + " parameters extracted");
            return true;
        }

        private string ExtractParameter(string _pattern)
        {
            if (string.IsNullOrEmpty(_pattern))
            {
                throw new ArgumentNullException(nameof(_pattern));
            }

            if (_pattern.Contains("{"))
            {
                if (_pattern.Contains("}"))
                {
                    int indexStart = _pattern.IndexOf('{');
                    int indexEnd = _pattern.LastIndexOf('}');
                    if ((indexEnd - 1) > indexStart)
                    {
                        return _pattern.Substring(indexStart, (indexEnd - indexStart + 1));
                    }
                }
            }

            return null;
        }

        private string ExtractParameterValue(string _url, string _pattern)
        {
            if (string.IsNullOrEmpty(_url))
            {
                throw new ArgumentNullException(nameof(_url));
            }

            if (string.IsNullOrEmpty(_pattern))
            {
                throw new ArgumentNullException(nameof(_pattern));
            }

            int index_start = _pattern.IndexOf('{');
            int index_end = _pattern.LastIndexOf('}');

            if ((index_end - 1) > index_start)
            {
                return _url.Substring(index_start, (index_end - 1));
            }

            return "";
        }
    }
}