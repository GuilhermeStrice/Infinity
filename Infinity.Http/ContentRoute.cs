using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Assign a method handler for when requests are received matching the supplied method and path.
    /// </summary>
    public class ContentRoute
    {
        /// <summary>
        /// Globally-unique identifier.
        /// </summary>
        [JsonPropertyOrder(-1)]
        public Guid GUID { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The pattern against which the raw URL should be matched.  
        /// </summary>
        [JsonPropertyOrder(0)]
        public string Path { get; set; } = null;

        /// <summary>
        /// Indicates whether or not the path specifies a directory.  If so, any matching URL will be handled by the specified handler.
        /// </summary>
        [JsonPropertyOrder(1)]
        public bool IsDirectory { get; set; } = false;

        /// <summary>
        /// User-supplied metadata.
        /// </summary>
        [JsonPropertyOrder(999)]
        public object Metadata { get; set; } = null;

        /// <summary>
        /// Create a new route object.
        /// </summary> 
        /// <param name="_path">The pattern against which the raw URL should be matched.</param>
        /// <param name="_is_directory">Indicates whether or not the path specifies a directory.  If so, any matching URL will be handled by the specified handler.</param> 
        /// <param name="_guid">Globally-unique identifier.</param>
        /// <param name="_metadata">User-supplied metadata.</param>
        public ContentRoute(string _path, bool _is_directory, Guid _guid = default, object _metadata = null)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            Path = _path.ToLower();
            IsDirectory = _is_directory;

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
