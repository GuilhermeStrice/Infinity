using Infinity.Core;
using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;

namespace Infinity.Http
{
    /// <summary>
    /// Response to an HTTP request.
    /// </summary>
    public class HttpResponse
    {
        /// <summary>
        /// UTC timestamp from when the response object was created.
        /// </summary>
        [JsonPropertyOrder(-5)]
        public Timestamp Timestamp { get; set; } = new Timestamp();

        /// <summary>
        /// The protocol and version.
        /// </summary>
        [JsonPropertyOrder(-4)]
        public string ProtocolVersion { get; set; } = null;

        /// <summary>
        /// The HTTP status code to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-3)]
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// The HTTP status description to return to the requestor (client).
        /// </summary>
        [JsonPropertyOrder(-2)]
        public string StatusDescription
        {
            get
            {
                return StatusCodeToDescription(StatusCode);
            }
        }

        /// <summary>
        /// User-supplied headers to include in the response.
        /// </summary>
        [JsonPropertyOrder(-1)]
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
        /// User-supplied content-type to include in the response.
        /// </summary>
        public string ContentType { get; set; } = string.Empty;

        /// <summary>
        /// The length of the supplied response data.
        /// </summary>
        public long ContentLength = 0;

        /// <summary>
        /// Indicates whether or not chunked transfer encoding should be indicated in the response. 
        /// </summary>
        public bool ChunkedTransfer { get; set; } = false;

        /// <summary>
        /// Boolean indicating if the response has been sent.
        /// </summary>
        public bool ResponseSent { get; set; } = false;

        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
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

                if (data != null && ContentLength > 0)
                {
                    data_as_bytes = ReadStreamFully(data);
                    if (data_as_bytes != null)
                    {
                        return Encoding.UTF8.GetString(data_as_bytes);
                    }
                }
                return null;
            }
        }

        /// <summary>
        /// Retrieve the response body sent using a Send() or SendAsync() method.
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

                if (data != null && ContentLength > 0)
                {
                    data_as_bytes = ReadStreamFully(data);
                    return data_as_bytes;
                }

                return null;
            }
        }

        /// <summary>
        /// Response data stream sent to the requestor.
        /// </summary>
        [JsonIgnore]
        public MemoryStream Data
        {
            get
            {
                return data;
            }
        }

        private HttpRequest request = null;
        private HttpListenerContext context = null;
        private HttpListenerResponse response = null;
        private Stream output_stream = null;
        private bool headers_set = false;

        private WebserverSettings settings = new WebserverSettings();

        private NameValueCollection headers = new NameValueCollection(StringComparer.InvariantCultureIgnoreCase);
        private byte[] data_as_bytes = null;
        private MemoryStream data = null;
        private ISerializationHelper serializer = null;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public HttpResponse()
        {

        }

        internal HttpResponse(HttpRequest _req, HttpListenerContext _ctx, WebserverSettings _settings, ISerializationHelper _serializer)
        {
            if (_req == null)
            {
                throw new ArgumentNullException(nameof(_req));
            }

            if (_ctx == null)
            {
                throw new ArgumentNullException(nameof(_ctx));
            }

            if (_settings == null)
            {
                throw new ArgumentNullException(nameof(_settings));
            }

            if (_serializer == null)
            {
                throw new ArgumentNullException(nameof(_serializer));
            }

            serializer = _serializer;
            request = _req;
            context = _ctx;
            response = context.Response;
            settings = _settings;

            output_stream = response.OutputStream;
        }

        /// <summary>
        /// Send headers and no data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(CancellationToken _token = default)
        {
            if (ChunkedTransfer)
            {
                throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            }

            return await SendInternalAsync(0, null, _token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send headers with a specified content length and no data to the requestor and terminate the connection.  Useful for HEAD requests where the content length must be set.
        /// </summary>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <param name="_content_length">Content length.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long _content_length, CancellationToken _token = default)
        {
            if (ChunkedTransfer)
            {
                throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            }

            ContentLength = _content_length;
            return await SendInternalAsync(0, null, _token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="data">Data.</param>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(string data, CancellationToken _token = default)
        {
            if (ChunkedTransfer)
            {
                throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            }

            if (string.IsNullOrEmpty(data))
            {
                return await SendInternalAsync(0, null, _token).ConfigureAwait(false);
            }

            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length, _token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return await SendInternalAsync(bytes.Length, ms, _token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate the connection.
        /// </summary>
        /// <param name="_data">Data.</param>
        /// <param name="token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(byte[] _data, CancellationToken token = default)
        {
            if (ChunkedTransfer)
            {
                throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            }

            if (_data == null || _data.Length < 1)
            {
                return await SendInternalAsync(0, null, token).ConfigureAwait(false);
            }

            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(_data, 0, _data.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return await SendInternalAsync(_data.Length, ms, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send headers and data to the requestor and terminate.
        /// </summary>
        /// <param name="_content_length">Number of bytes to send.</param>
        /// <param name="_stream">Stream containing the data.</param>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> Send(long _content_length, Stream _stream, CancellationToken _token = default)
        {
            if (ChunkedTransfer)
            {
                throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");
            }

            return await SendInternalAsync(_content_length, _stream, _token);
        }

        /// <summary>
        /// Send headers (if not already sent) and a chunk of data using chunked transfer-encoding, and keep the connection in-tact.
        /// </summary>
        /// <param name="_chunk">Chunk of data.</param>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendChunk(byte[] _chunk, CancellationToken _token = default)
        {
            if (!ChunkedTransfer)
            {
                throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            }

            if (!headers_set)
            {
                SendHeaders();
            }

            if (_chunk != null && _chunk.Length > 0)
            {
                ContentLength += _chunk.Length;
            }

            try
            {
                if (_chunk == null || _chunk.Length < 1)
                {
                    _chunk = Array.Empty<byte>();
                }

                await output_stream.WriteAsync(_chunk, 0, _chunk.Length, _token).ConfigureAwait(false);
                await output_stream.FlushAsync(_token).ConfigureAwait(false);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Send headers (if not already sent) and the final chunk of data using chunked transfer-encoding and terminate the connection.
        /// </summary>
        /// <param name="_chunk">Chunk of data.</param>
        /// <param name="_token">Cancellation token useful for canceling the request.</param>
        /// <returns>True if successful.</returns>
        public async Task<bool> SendFinalChunk(byte[] _chunk, CancellationToken _token = default)
        {
            if (!ChunkedTransfer)
            {
                throw new IOException("Response is not configured to use chunked transfer-encoding.  Set ChunkedTransfer to true first, otherwise use Send().");
            }

            if (!headers_set)
            {
                SendHeaders();
            }

            if (_chunk != null && _chunk.Length > 0)
            {
                ContentLength += _chunk.Length;
            }

            try
            {
                if (_chunk != null && _chunk.Length > 0)
                {
                    await output_stream.WriteAsync(_chunk, 0, _chunk.Length, _token).ConfigureAwait(false);
                }

                byte[] end_chunk = Array.Empty<byte>();
                await output_stream.WriteAsync(end_chunk, 0, end_chunk.Length, _token).ConfigureAwait(false);

                await output_stream.FlushAsync(_token).ConfigureAwait(false);
                output_stream.Close();

                if (response != null)
                {
                    response.Close();
                }

                ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void SendHeaders()
        {
            if (headers_set)
            {
                throw new IOException("Headers already sent.");
            }

            response.ContentLength64 = ContentLength;
            response.StatusCode = StatusCode;
            response.StatusDescription = GetStatusDescription(StatusCode);
            response.SendChunked = ChunkedTransfer;
            response.ContentType = ContentType;
            response.KeepAlive = false;

            if (Headers != null && Headers.Count > 0)
            {
                for (int i = 0; i < Headers.Count; i++)
                {
                    string key = Headers.GetKey(i);
                    string[] vals = Headers.GetValues(i);

                    if (vals == null || vals.Length < 1)
                    {
                        response.AddHeader(key, null);
                    }
                    else
                    {
                        for (int j = 0; j < vals.Length; j++)
                        {
                            response.AddHeader(key, vals[j]);
                        }
                    }
                }
            }

            if (settings.Headers.DefaultHeaders != null && settings.Headers.DefaultHeaders.Count > 0)
            {
                foreach (KeyValuePair<string, string> header in settings.Headers.DefaultHeaders)
                {
                    if (Headers.Get(header.Key) != null || Headers.AllKeys.Contains(header.Key))
                    {
                        // already present
                    }
                    else
                    {
                        response.AddHeader(header.Key, header.Value);
                    }
                }
            }

            headers_set = true;
        }

        private string GetStatusDescription(int _status_code)
        {
            switch (_status_code)
            {
                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Moved Temporarily";
                case 304:
                    return "Not Modified";
                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 408:
                    return "Request Timeout";
                case 429:
                    return "Too Many Requests";
                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 503:
                    return "Service Unavailable";
                default:
                    return "Unknown Status";
            }
        }

        private byte[] ReadStreamFully(Stream input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!input.CanRead) throw new InvalidOperationException("Input stream is not readable");

            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;

                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }

                byte[] ret = ms.ToArray();
                return ret;
            }
        }

        private async Task<bool> SendInternalAsync(long contentLength, Stream stream, CancellationToken token = default)
        {
            if (ChunkedTransfer) throw new IOException("Response is configured to use chunked transfer-encoding.  Use SendChunk() and SendFinalChunk().");

            if (ContentLength == 0 && contentLength > 0) ContentLength = contentLength;

            if (!headers_set) SendHeaders();

            try
            {
                if (request.Method != HttpMethod.HEAD)
                {
                    if (stream != null && stream.CanRead && contentLength > 0)
                    {
                        long bytesRemaining = contentLength;

                        data = new MemoryStream();

                        while (bytesRemaining > 0)
                        {
                            int bytesRead = 0;
                            byte[] buffer = new byte[settings.IO.StreamBufferSize];
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                            if (bytesRead > 0)
                            {
                                await data.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                                await output_stream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);
                                bytesRemaining -= bytesRead;
                            }
                        }

                        stream.Close();
                        stream.Dispose();

                        data.Seek(0, SeekOrigin.Begin);
                    }
                }

                await output_stream.FlushAsync(token).ConfigureAwait(false);
                output_stream.Close();

                if (response != null) response.Close();

                ResponseSent = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private string StatusCodeToDescription(int statusCode)
        {
            //
            // Helpful links:
            // https://developer.mozilla.org/en-US/docs/Web/HTTP/Status
            // https://en.wikipedia.org/wiki/List_of_HTTP_status_codes
            // 

            switch (statusCode)
            {
                case 100:
                    return "Continue";
                case 101:
                    return "Switching Protocols";
                case 102:
                    return "Processing";
                case 103:
                    return "Early Hints";

                case 200:
                    return "OK";
                case 201:
                    return "Created";
                case 202:
                    return "Accepted";
                case 203:
                    return "Non-Authoritative Information";
                case 204:
                    return "No Contact";
                case 205:
                    return "Reset Content";
                case 206:
                    return "Partial Content";
                case 207:
                    return "Multi-Status";
                case 208:
                    return "Already Reported";
                case 226:
                    return "IM Used";

                case 300:
                    return "Multiple Choices";
                case 301:
                    return "Moved Permanently";
                case 302:
                    return "Found";
                case 303:
                    return "See Other";
                case 304:
                    return "Not Modified";
                case 305:
                    return "Use Proxy";
                case 306:
                    return "Switch Proxy";
                case 307:
                    return "Temporary Redirect";
                case 308:
                    return "Permanent Redirect";

                case 400:
                    return "Bad Request";
                case 401:
                    return "Unauthorized";
                case 402:
                    return "Payment Required";
                case 403:
                    return "Forbidden";
                case 404:
                    return "Not Found";
                case 405:
                    return "Method Not Allowed";
                case 406:
                    return "Not Acceptable";
                case 407:
                    return "Proxy Authentication Required";
                case 408:
                    return "Request Timeout";
                case 409:
                    return "Conflict";
                case 410:
                    return "Gone";
                case 411:
                    return "Length Required";
                case 412:
                    return "Precondition Failed";
                case 413:
                    return "Payload too Large";
                case 414:
                    return "URI Too Long";
                case 415:
                    return "Unsupported Media Type";
                case 416:
                    return "Range Not Satisfiable";
                case 417:
                    return "Expectation Failed";
                case 418:
                    return "I'm a teapot";
                case 421:
                    return "Misdirected Request";
                case 422:
                    return "Unprocessable Content";
                case 423:
                    return "Locked";
                case 424:
                    return "Failed Dependency";
                case 425:
                    return "Too Early";
                case 426:
                    return "Upgrade Required";
                case 428:
                    return "Precondition Required";
                case 429:
                    return "Too Many Requests";
                case 431:
                    return "Request Header Fields Too Large";
                case 451:
                    return "Unavailable For Legal Reasons";

                case 500:
                    return "Internal Server Error";
                case 501:
                    return "Not Implemented";
                case 502:
                    return "Bad Gateway";
                case 503:
                    return "Service Unavailable";
                case 504:
                    return "Gateway Timeout";
                case 505:
                    return "HTTP Version Not Supported";
                case 506:
                    return "Variant Also Negotiates";
                case 507:
                    return "Insufficient Storage";
                case 508:
                    return "Loop Detected";
                case 510:
                    return "Not Extended";
                case 511:
                    return "Network Authentication Required";
            }

            return "Unknown";
        }
    }
}