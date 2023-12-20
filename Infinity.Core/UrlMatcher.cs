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

        /// <summary>
        /// Method to invoke to send log messages.
        /// </summary>
        public Action<string> Logger = null;

        private string _Header = "[UrlParser] ";

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
        /// <param name="url">The URL to evaluate.</param>
        /// <param name="pattern">The pattern used to evaluate the URL.</param>
        /// <param name="vals">Dictionary containing keys and values.</param>
        /// <returns>True if matched.</returns>
        public bool Match(string url, string pattern, out NameValueCollection vals)
        {
            vals = null;
            if (String.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
            if (String.IsNullOrEmpty(pattern)) throw new ArgumentNullException(nameof(pattern));

            vals = new NameValueCollection();
            string[] urlParts = url.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string[] patternParts = pattern.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            if (urlParts.Length != patternParts.Length) return false;

            for (int i = 0; i < urlParts.Length; i++)
            {
                string paramName = ExtractParameter(patternParts[i]);

                if (String.IsNullOrEmpty(paramName))
                {
                    // no pattern
                    if (!urlParts[i].Equals(patternParts[i]))
                    {
                        Logger?.Invoke(_Header + "content mismatch at position " + i);
                        vals = null;
                        return false;
                    }
                }
                else
                {
                    Logger?.Invoke(_Header + paramName.Replace("{", "").Replace("}", "") + ": " + urlParts[i]);
                    vals.Add(
                        paramName.Replace("{", "").Replace("}", ""),
                        urlParts[i]);
                }
            }

            Logger?.Invoke(_Header + "match detected, " + vals.Count + " parameters extracted");
            return true;
        }

        private string ExtractParameter(string pattern)
        {
            if (String.IsNullOrEmpty(pattern)) throw new ArgumentNullException(nameof(pattern));

            if (pattern.Contains("{"))
            {
                if (pattern.Contains("}"))
                {
                    int indexStart = pattern.IndexOf('{');
                    int indexEnd = pattern.LastIndexOf('}');
                    if ((indexEnd - 1) > indexStart)
                    {
                        return pattern.Substring(indexStart, (indexEnd - indexStart + 1));
                    }
                }
            }

            return null;
        }

        private string ExtractParameterValue(string url, string pattern)
        {
            if (String.IsNullOrEmpty(url)) throw new ArgumentNullException(nameof(url));
            if (String.IsNullOrEmpty(pattern)) throw new ArgumentNullException(nameof(pattern));

            int indexStart = pattern.IndexOf('{');
            int indexEnd = pattern.LastIndexOf('}');

            if ((indexEnd - 1) > indexStart)
            {
                return url.Substring(indexStart, (indexEnd - 1));
            }

            return "";
        }
    }
}