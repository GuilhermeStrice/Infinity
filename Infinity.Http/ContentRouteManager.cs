namespace Infinity.Http
{
    /// <summary>
    /// Content route manager.  Content routes are used for GET and HEAD requests to specific files or entire directories.
    /// </summary>
    public class ContentRouteManager
    {
        /// <summary>
        /// Base directory for files and directories accessible via content routes.
        /// </summary>
        public string BaseDirectory
        {
            get
            {
                return base_directory;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    base_directory = AppDomain.CurrentDomain.BaseDirectory;
                }
                else
                {
                    if (!Directory.Exists(value))
                    {
                        throw new DirectoryNotFoundException("The requested directory '" + value + "' was not found or not accessible.");
                    }

                    base_directory = value;
                }
            }
        }

        /// <summary>
        /// The FileMode value to use when accessing files within a content route via a FileStream.  Default is FileMode.Open.
        /// </summary>
        public FileMode ContentFileMode { get; set; } = FileMode.Open;

        /// <summary>
        /// The FileAccess value to use when accessing files within a content route via a FileStream.  Default is FileAccess.Read.
        /// </summary>
        public FileAccess ContentFileAccess { get; set; } = FileAccess.Read;

        /// <summary>
        /// The FileShare value to use when accessing files within a content route via a FileStream.  Default is FileShare.Read.
        /// </summary>
        public FileShare ContentFileShare { get; set; } = FileShare.Read;

        /// <summary>
        /// Content route handler.
        /// </summary>
        public Func<HttpContext, Task> Handler
        {
            get
            {
                return handler;
            }
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(Handler));
                }

                handler = value;
            }
        }

        private List<ContentRoute> routes = new List<ContentRoute>();
        private readonly object @lock = new object();
        private string base_directory = AppDomain.CurrentDomain.BaseDirectory;
        private Func<HttpContext, Task> handler = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary> 
        public ContentRouteManager()
        {
            handler = HandlerInternal;
        }

        /// <summary>
        /// Add a route.
        /// </summary>
        /// <param name="_path">URL path, i.e. /path/to/resource.</param>
        /// <param name="_is_directory">True if the path represents a directory.</param>
        /// <param name="_guid">Globally-unique identifier.</param>
        /// <param name="_metadata">User-supplied metadata.</param>
        public void Add(string _path, bool _is_directory, Guid _guid = default, object _metadata = null)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            Add(new ContentRoute(_path, _is_directory, _guid, _metadata));
        }

        /// <summary>
        /// Remove a route.
        /// </summary>
        /// <param name="_path">URL path.</param>
        public void Remove(string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            ContentRoute r = Get(_path);
            if (r == null)
            {
                return;
            }

            lock (@lock)
            {
                routes.Remove(r);
            }
                 
            return;
        }

        /// <summary>
        /// Retrieve a content route.
        /// </summary>
        /// <param name="_path">URL path.</param>
        /// <returns>ContentRoute if the route exists, otherwise null.</returns>
        public ContentRoute Get(string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }
            
            _path = _path.ToLower();
            if (!_path.StartsWith("/"))
            {
                _path = "/" + _path;
            }

            if (!_path.EndsWith("/"))
            {
                _path = _path + "/";
            }

            lock (@lock)
            {
                foreach (ContentRoute curr in routes)
                {
                    if (curr.IsDirectory)
                    {
                        if (_path.StartsWith(curr.Path.ToLower()))
                        {
                            return curr;
                        }
                    }
                    else
                    {
                        if (_path.Equals(curr.Path.ToLower()))
                        {
                            return curr;
                        }
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Check if a content route exists.
        /// </summary>
        /// <param name="_path">URL path.</param>
        /// <returns>True if exists.</returns>
        public bool Exists(string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }
             
            _path = _path.ToLower();
            if (!_path.StartsWith("/"))
            {
                _path = "/" + _path;
            }

            lock (@lock)
            {
                foreach (ContentRoute curr in routes)
                {
                    if (curr.IsDirectory)
                    {
                        if (_path.StartsWith(curr.Path.ToLower()))
                        {
                            return true;
                        }
                    }
                    else
                    {
                        if (_path.Equals(curr.Path.ToLower()))
                        {
                            return true;
                        }
                    }
                }
            }
             
            return false;
        }

        /// <summary>
        /// Retrieve a content route.
        /// </summary>
        /// <param name="_path">URL path.</param>
        /// <param name="_route">Matching route.</param>
        /// <returns>True if a match exists.</returns>
        public bool Match(string _path, out ContentRoute _route)
        {
            _route = null;
            if (string.IsNullOrEmpty(_path))
            {
                throw new ArgumentNullException(nameof(_path));
            }

            _path = _path.ToLower(); 
            string dir_path = _path;
            if (!dir_path.EndsWith("/"))
            {
                dir_path = dir_path + "/";
            }

            lock (@lock)
            {
                foreach (ContentRoute curr in routes)
                {
                    if (curr.IsDirectory)
                    {
                        if (dir_path.StartsWith(curr.Path.ToLower()))
                        {
                            _route = curr;
                            return true;
                        }
                    }
                    else
                    {
                        if (_path.Equals(curr.Path.ToLower()))
                        {
                            _route = curr;
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private void Add(ContentRoute _route)
        {
            if (_route == null)
            {
                throw new ArgumentNullException(nameof(_route));
            }
            
            _route.Path = _route.Path.ToLower();
            if (!_route.Path.StartsWith("/"))
            {
                _route.Path = "/" + _route.Path;
            }

            if (_route.IsDirectory && !_route.Path.EndsWith("/"))
            {
                _route.Path = _route.Path + "/";
            }

            if (Exists(_route.Path))
            { 
                return;
            }

            lock (@lock)
            {
                routes.Add(_route); 
            }
        }

        private async Task HandlerInternal(HttpContext _ctx)
        {
            if (_ctx == null)
            {
                throw new ArgumentNullException(nameof(_ctx));
            }

            if (_ctx.Request == null)
            {
                throw new ArgumentNullException(nameof(_ctx.Request));
            }

            if (_ctx.Response == null)
            {
                throw new ArgumentNullException(nameof(_ctx.Response));
            }

            if (_ctx.Request.Method != HttpMethod.GET
                && _ctx.Request.Method != HttpMethod.HEAD)
            {
                Set500Response(_ctx);
                await _ctx.Response.Send(_ctx.Token).ConfigureAwait(false);
                return;
            }

            string file_path = _ctx.Request.Url.RawWithoutQuery;
            if (!string.IsNullOrEmpty(file_path))
            {
                while (file_path.StartsWith("/"))
                {
                    file_path = file_path.Substring(1);
                }
            }

            string base_directory = BaseDirectory;
            base_directory = base_directory.Replace("\\", "/");
            if (!base_directory.EndsWith("/"))
            {
                base_directory += "/";
            }

            file_path = base_directory + file_path;
            file_path = file_path.Replace("+", " ").Replace("%20", " ");

            if (!File.Exists(file_path))
            {
                Set404Response(_ctx);
                await _ctx.Response.Send(_ctx.Token).ConfigureAwait(false);
                return;
            }

            FileInfo fi = new FileInfo(file_path);
            long content_length = fi.Length;

            if (_ctx.Request.Method == HttpMethod.GET)
            {
                FileStream fs = new FileStream(file_path, ContentFileMode, ContentFileAccess, ContentFileShare);
                _ctx.Response.StatusCode = 200;
                _ctx.Response.ContentLength = content_length;
                _ctx.Response.ContentType = GetContentType(file_path);
                await _ctx.Response.Send(content_length, fs, _ctx.Token).ConfigureAwait(false);
                return;
            }
            else if (_ctx.Request.Method == HttpMethod.HEAD)
            {
                _ctx.Response.StatusCode = 200;
                _ctx.Response.ContentLength = content_length;
                _ctx.Response.ContentType = GetContentType(file_path);
                await _ctx.Response.Send(content_length, _ctx.Token).ConfigureAwait(false);
                return;
            }
            else
            {
                Set500Response(_ctx);
                await _ctx.Response.Send(_ctx.Token).ConfigureAwait(false);
                return;
            }
        }

        private string GetContentType(string _path)
        {
            if (string.IsNullOrEmpty(_path))
            {
                return "application/octet-stream";
            }

            int idx = _path.LastIndexOf(".");
            if (idx >= 0)
            {
                return MimeTypes.GetFromExtension(_path.Substring(idx));
            }

            return "application/octet-stream";
        }

        private void Set404Response(HttpContext _ctx)
        {
            _ctx.Response.StatusCode = 404;
            _ctx.Response.ContentLength = 0;
        }

        private void Set500Response(HttpContext _ctx)
        {
            _ctx.Response.StatusCode = 500;
            _ctx.Response.ContentLength = 0;
        }
    }
}
