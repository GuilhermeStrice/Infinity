using Infinity.Core;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Data extracted from an incoming HTTP request.
    /// </summary>
    public class HttpRequest
    {
        /// <summary>
        /// UTC timestamp from when the request object was received.
        /// </summary>
        [JsonPropertyOrder(-11)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// Globally-unique identifier for the request.
        /// </summary>
        public Guid Guid { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Thread ID on which the request exists.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public int ThreadId { get; set; } = Thread.CurrentThread.ManagedThreadId;

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-9)]
        public string ProtocolVersion { get; set; } = null;

        /// <summary>
        /// Source (requestor) IP and port information.
        /// </summary>
        [JsonPropertyOrder(-8)]
        public SourceDetails Source { get; set; } = new SourceDetails();

        /// <summary>
        /// Destination IP and port information.
        /// </summary>
        [JsonPropertyOrder(-7)]
        public DestinationDetails Destination { get; set; } = new DestinationDetails();

        /// <summary>
        /// The HTTP method used in the request.
        /// </summary>
        [JsonPropertyOrder(-6)]
        public HttpMethod Method { get; set; } = HttpMethod.GET;

        /// <summary>
        /// The string version of the HTTP method, useful if Method is UNKNOWN.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public string MethodRaw { get; set; } = null;

        /// <summary>
        /// URL details.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public UrlDetails Url { get; set; } = new UrlDetails();

        /// <summary>
        /// Query details.
        /// </summary>
        [JsonPropertyOrder(-3)]
        public QueryDetails Query { get; set; } = new QueryDetails();

        /// <summary>
        /// The headers found in the request.
        /// </summary>
        [JsonPropertyOrder(-2)]
        public NameValueCollection Headers
        {
            get
            {
                return headers;
            }
            set
            {
                if (value == null)
                {
                    headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
                }
                else
                {
                    headers = value;
                }
            }
        }

        /// <summary>
        /// Specifies whether or not the client requested HTTP keepalives.
        /// </summary>
        public bool Keepalive { get; set; } = false;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding was detected.
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been gzip compressed.
        /// </summary>
        public bool Gzip { get; set; } = false;

        /// <summary>
        /// Indicates whether or not the payload has been deflate compressed.
        /// </summary>
        public bool Deflate { get; set; } = false;

        /// <summary>
        /// The useragent specified in the request.
        /// </summary>
        public string Useragent { get; set; } = null;

        /// <summary>
        /// The content type as specified by the requestor (client).
        /// </summary>
        [JsonPropertyOrder(990)]
        public string ContentType { get; set; } = null;

        /// <summary>
        /// The number of bytes in the request body.
        /// </summary>
        [JsonPropertyOrder(991)]
        public long ContentLength { get; set; } = 0;

        /// <summary>
        /// The stream from which to read the request body sent by the requestor (client).
        /// </summary>
        [JsonIgnore]
        public Stream Data { get; set; } = new MemoryStream();

        /// <summary>
        /// Retrieve the request body as a byte array.  This will fully read the stream. 
        /// </summary>
        [JsonIgnore]
        public byte[] DataAsBytes
        {
            get
            {
                if (data_as_bytes != null)
                {
                    return data_as_bytes;
                }

                if (Data != null && ContentLength > 0)
                {
                    data_as_bytes = ReadStreamFully(Data);
                    return data_as_bytes;
                }

                return null;
            }
        }

        /// <summary>
        /// Retrieve the request body as a string.  This will fully read the stream.
        /// </summary>
        [JsonIgnore]
        public string DataAsString
        {
            get
            {
                if (data_as_bytes != null)
                {
                    return Encoding.UTF8.GetString(data_as_bytes);
                }

                if (Data != null && ContentLength > 0)
                {
                    data_as_bytes = ReadStreamFully(Data);
                    if (data_as_bytes != null)
                    {
                        return Encoding.UTF8.GetString(data_as_bytes);
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// The original HttpListenerContext from which the HttpRequest was constructed.
        /// </summary>
        [JsonIgnore]
        public HttpListenerContext ListenerContext { get; set; }

        private int stream_buffer_size = 65536;
        private Uri uri = null;
        private byte[] data_as_bytes = null;
        private ISerializationHelper serializer = null;
        private NameValueCollection headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// HTTP request.
        /// </summary>
        public HttpRequest()
        {
        }

        /// <summary>
        /// HTTP request.
        /// Instantiate the object using an HttpListenerContext.
        /// </summary>
        /// <param name="_ctx">HttpListenerContext.</param>
        /// <param name="serializer">Serialization helper.</param>
        public HttpRequest(HttpListenerContext _ctx, ISerializationHelper _serializer)
        {
            if (_ctx == null)
            {
                throw new ArgumentNullException(nameof(_ctx));
            }

            if (_ctx.Request == null)
            {
                throw new ArgumentNullException(nameof(_ctx.Request));
            }

            if (serializer == null)
            {
                throw new ArgumentNullException(nameof(serializer));
            }

            this.serializer = _serializer;

            ListenerContext = _ctx;
            Keepalive = _ctx.Request.KeepAlive;
            ContentLength = _ctx.Request.ContentLength64;
            Useragent = _ctx.Request.UserAgent;
            ContentType = _ctx.Request.ContentType;

            uri = new Uri(_ctx.Request.Url.ToString().Trim());

            ProtocolVersion = "HTTP/" + _ctx.Request.ProtocolVersion.ToString();
            Source = new SourceDetails(_ctx.Request.RemoteEndPoint.Address.ToString(), _ctx.Request.RemoteEndPoint.Port);
            Destination = new DestinationDetails(_ctx.Request.LocalEndPoint.Address.ToString(), _ctx.Request.LocalEndPoint.Port, uri.Host);
            Url = new UrlDetails(_ctx.Request.Url.ToString().Trim(), _ctx.Request.RawUrl.ToString().Trim());
            Query = new QueryDetails(Url.Full);
            MethodRaw = _ctx.Request.HttpMethod;

            try
            {
                Method = (HttpMethod)Enum.Parse(typeof(HttpMethod), _ctx.Request.HttpMethod, true);
            }
            catch (Exception)
            {
                Method = HttpMethod.UNKNOWN;
            }

            Headers = _ctx.Request.Headers;

            for (int i = 0; i < Headers.Count; i++)
            {
                string key = Headers.GetKey(i);
                string[] vals = Headers.GetValues(i);

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                if (vals == null || vals.Length < 1)
                {
                    continue;
                }

                if (key.ToLower().Equals("transfer-encoding"))
                {
                    if (vals.Contains("chunked", StringComparer.InvariantCultureIgnoreCase))
                    {
                        ChunkedTransfer = true;
                    }

                    if (vals.Contains("gzip", StringComparer.InvariantCultureIgnoreCase))
                    {
                        Gzip = true;
                    }
                    if (vals.Contains("deflate", StringComparer.InvariantCultureIgnoreCase))
                    {
                        Deflate = true;
                    }
                }
                else if (key.ToLower().Equals("x-amz-content-sha256"))
                {
                    if (vals.Contains("streaming", StringComparer.InvariantCultureIgnoreCase))
                    {
                        ChunkedTransfer = true;
                    }
                }
            }

            Data = _ctx.Request.InputStream;
        }

        /// <summary>
        /// For chunked transfer-encoded requests, read the next chunk.
        /// It is strongly recommended that you use the ChunkedTransfer parameter before invoking this method.
        /// </summary>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <returns>Chunk.</returns>
        public async Task<Chunk> ReadChunk(CancellationToken _token = default)
        {
            Chunk chunk = new Chunk();

            #region Get-Length-and-Metadata

            byte[] buffer = new byte[1];
            byte[] len_bytes = null;
            int bytes_read;

            while (true)
            {
                bytes_read = await Data.ReadAsync(buffer, 0, buffer.Length, _token).ConfigureAwait(false);
                if (bytes_read > 0)
                {
                    len_bytes = AppendBytes(len_bytes, buffer);
                    string len_str = Encoding.UTF8.GetString(len_bytes);

                    if (len_bytes[len_bytes.Length - 1] == 10)
                    {
                        len_str = len_str.Trim();

                        if (len_str.Contains(";"))
                        {
                            string[] len_parts = len_str.Split(new char[] { ';' }, 2);
                            chunk.Length = int.Parse(len_parts[0], NumberStyles.HexNumber);
                            if (len_parts.Length >= 2) chunk.Metadata = len_parts[1];
                        }
                        else
                        {
                            chunk.Length = int.Parse(len_str, NumberStyles.HexNumber);
                        }

                        break;
                    }
                }
            }

            #endregion

            #region Get-Data

            int bytes_remaining = chunk.Length;

            if (chunk.Length > 0)
            {
                chunk.IsFinal = false;
                using (MemoryStream ms = new MemoryStream())
                {
                    while (true)
                    {
                        if (bytes_remaining > stream_buffer_size)
                        {
                            buffer = new byte[stream_buffer_size];
                        }
                        else
                        {
                            buffer = new byte[bytes_remaining];
                        }

                        bytes_read = await Data.ReadAsync(buffer, 0, buffer.Length, _token).ConfigureAwait(false);

                        if (bytes_read > 0)
                        {
                            await ms.WriteAsync(buffer, 0, bytes_read);
                            bytes_remaining -= bytes_read;
                        }

                        if (bytes_remaining == 0)
                        {
                            break;
                        }
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    chunk.Data = ms.ToArray();
                }
            }
            else
            {
                chunk.IsFinal = true;
            }

            #endregion

            #region Get-Trailing-CRLF

            buffer = new byte[1];

            while (true)
            {
                bytes_read = await Data.ReadAsync(buffer, 0, buffer.Length, _token).ConfigureAwait(false);
                if (bytes_read > 0)
                {
                    if (buffer[0] == 10)
                    {
                        break;
                    }
                }
            }

            #endregion

            return chunk;
        }

        /// <summary>
        /// Determine if a header exists.
        /// </summary>
        /// <param name="_key">Header key.</param>
        /// <returns>True if exists.</returns>
        public bool HeaderExists(string _key)
        {
            if (string.IsNullOrEmpty(_key))
            {
                throw new ArgumentNullException(nameof(_key));
            }

            if (Headers != null)
            {
                return Headers.AllKeys.Any(k => k.ToLower().Equals(_key.ToLower()));
            }

            return false;
        }

        /// <summary>
        /// Determine if a querystring entry exists.
        /// </summary>
        /// <param name="_key">Querystring key.</param>
        /// <returns>True if exists.</returns>
        public bool QuerystringExists(string _key)
        {
            if (string.IsNullOrEmpty(_key))
            {
                throw new ArgumentNullException(nameof(_key));
            }

            if (Query != null && Query.Elements != null)
            {
                return Query.Elements.AllKeys.Any(k => k.ToLower().Equals(_key.ToLower()));
            }

            return false;
        }

        /// <summary>
        /// Retrieve a header (or querystring) value.
        /// </summary>
        /// <param name="_key">Key.</param>
        /// <returns>Value.</returns>
        public string RetrieveHeaderValue(string _key)
        {
            if (string.IsNullOrEmpty(_key))
            {
                throw new ArgumentNullException(nameof(_key));
            }

            if (Headers != null)
            {
                return Headers.Get(_key);
            }

            return null;
        }

        /// <summary>
        /// Retrieve a querystring value.
        /// </summary>
        /// <param name="_key">Key.</param>
        /// <returns>Value.</returns>
        public string RetrieveQueryValue(string _key)
        {
            if (string.IsNullOrEmpty(_key))
            {
                throw new ArgumentNullException(nameof(_key));
            }

            if (Query != null && Query.Elements != null)
            {
                string val = Query.Elements.Get(_key);
                if (!string.IsNullOrEmpty(val))
                {
                    val = WebUtility.UrlDecode(val);
                }

                return val;
            }

            return null;
        }

        private byte[] AppendBytes(byte[] _orig, byte[] _append)
        {
            if (_orig == null && _append == null)
            {
                return null;
            }

            byte[] ret;

            if (_append == null)
            {
                ret = new byte[_orig.Length];
                Buffer.BlockCopy(_orig, 0, ret, 0, _orig.Length);
                return ret;
            }

            if (_orig == null)
            {
                ret = new byte[_append.Length];
                Buffer.BlockCopy(_append, 0, ret, 0, _append.Length);
                return ret;
            }

            ret = new byte[_orig.Length + _append.Length];
            Buffer.BlockCopy(_orig, 0, ret, 0, _orig.Length);
            Buffer.BlockCopy(_append, 0, ret, _orig.Length, _append.Length);
            return ret;
        }

        private byte[] StreamToBytes(Stream _input)
        {
            if (_input == null)
            {
                throw new ArgumentNullException(nameof(_input));
            }

            if (!_input.CanRead)
            {
                throw new InvalidOperationException("Input stream is not readable");
            }

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = _input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                return ms.ToArray();
            }
        }

        private void ReadStreamFully()
        {
            if (Data == null)
            {
                return;
            }

            if (!Data.CanRead)
            {
                return;
            }

            if (data_as_bytes == null)
            {
                if (!ChunkedTransfer)
                {
                    data_as_bytes = StreamToBytes(Data);
                }
                else
                {
                    while (true)
                    {
                        Chunk chunk = ReadChunk().Result;
                        if (chunk.Data != null && chunk.Data.Length > 0)
                        {
                            data_as_bytes = AppendBytes(data_as_bytes, chunk.Data);
                        }

                        if (chunk.IsFinal)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private byte[] ReadStreamFully(Stream _input)
        {
            if (_input == null)
            {
                throw new ArgumentNullException(nameof(_input));
            }

            if (!_input.CanRead)
            {
                throw new InvalidOperationException("Input stream is not readable");
            }

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = _input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                byte[] ret = ms.ToArray();
                return ret;
            }
        }
    }
}