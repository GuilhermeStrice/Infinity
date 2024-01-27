using System.Net;
using System.Net.Sockets;

namespace Infinity.SNTP
{
    public class NtpClient
    {
        public static readonly string DefaultHost = "pool.ntp.org";

        public const int DefaultPort = 123;

        public static readonly EndPoint DefaultEndpoint = new DnsEndPoint(DefaultHost, DefaultPort);

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        public TimeSpan Timeout { get; init; }

        private readonly EndPoint endpoint;

        private Socket CreateSocket()
        {
            var socket = new Socket(SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveTimeout = Convert.ToInt32(Timeout.TotalMilliseconds),
            };

            return socket;
        }

        public NtpClient(EndPoint _endpoint, TimeSpan? _timeout = default)
        {
            endpoint = _endpoint;
            Timeout = _timeout ?? DefaultTimeout;
        }

        public NtpClient() : this(DefaultEndpoint)
        {
        }

        public NtpClient(IPAddress _ip, TimeSpan? _timeout = null, int? _port = null) :
            this(new IPEndPoint(_ip, _port ?? DefaultPort), _timeout)
        {
        }

        public NtpClient(string _host, TimeSpan? _timeout = null, int? _port = null) :
            this(new DnsEndPoint(_host, _port ?? DefaultPort), _timeout)
        {
        }

        private volatile NtpClock last;

        public NtpClock Last => last;

        private NtpClock Update(NtpRequest _request, byte[] _buffer, int _length)
        {
            var response = NtpResponse.FromPacket(NtpPacket.FromBytes(_buffer, _length));
            if (!response.Matches(_request))
            {
                throw new NtpException("Response does not match the request.");
            }

            var time = new NtpClock(response);
            if (time.Synchronized || last == null)
            {
                last = time;
            }

            return time;
        }

        private Socket Connect()
        {
            var socket = CreateSocket();
            try
            {
                socket.Connect(endpoint);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
            return socket;
        }

        public NtpClock Query()
        {
            using var socket = Connect();
            var request = new NtpRequest();
            socket.Send(request.ToPacket().ToBytes());
            var buffer = new byte[160];
            var length = socket.Receive(buffer);
            return Update(request, buffer, length);
        }

        private async Task<Socket> ConnectAsync(CancellationToken _token)
        {
            var socket = CreateSocket();
            try
            {
                await socket.ConnectAsync(endpoint, _token).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
            return socket;
        }

        public async Task<NtpClock> QueryAsync(CancellationToken _token = default)
        {
            using var socket = await ConnectAsync(_token).ConfigureAwait(false);
            var request = new NtpRequest();
            var flags = SocketFlags.None;
            var rcvbuff = new byte[160];

            await socket.SendAsync(request.ToPacket().ToBytes(), flags, _token).ConfigureAwait(false);
            int length = await socket.ReceiveAsync(rcvbuff, flags, _token).ConfigureAwait(false);

            return Update(request, rcvbuff, length);
        }
    }
}