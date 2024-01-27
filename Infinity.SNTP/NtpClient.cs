using System.Net;
using System.Net.Sockets;

namespace Infinity.SNTP
{
    internal class StateSync
    {
        public NtpRequest Request;
        public byte[] Buffer;
    }

    public class NtpClient
    {
        public static readonly IPAddress DefaultHost = Dns.GetHostAddresses("pool.ntp.org")[0];

        public const int DefaultPort = 123;

        public static readonly EndPoint DefaultEndpoint = new IPEndPoint(DefaultHost, DefaultPort);

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

        public TimeSpan Timeout { get; init; }

        public event Action<NtpClock> OnNtpReceived;

        private EndPoint endpoint;

        private Socket socket;

        public NtpClient(EndPoint _endpoint, TimeSpan? _timeout = default)
        {
            endpoint = _endpoint;
            Timeout = _timeout ?? DefaultTimeout;

            socket = new Socket(SocketType.Dgram, ProtocolType.Udp)
            {
                ReceiveTimeout = Convert.ToInt32(Timeout.TotalMilliseconds),
            };
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

        public void Query()
        {
            var request = new NtpRequest();
            var buffer = request.ToPacket().ToBytes();

            var state_sync = new StateSync();
            state_sync.Request = request;

            try
            {
                socket.BeginSendTo(buffer, 0, buffer.Length, SocketFlags.None, endpoint, HandleSendTo, state_sync);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new NtpException($"Something happened while trying to begin the send operation : {message}");
            }
        }

        private void HandleSendTo(IAsyncResult result)
        {
            try
            {
                socket.EndSendTo(result);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new NtpException($"Something happened while trying to end the send operation : {message}");
            }

            var state_sync = (StateSync)result.AsyncState;

            var buffer = new byte[160];
            state_sync.Buffer = buffer;

            try
            {
                socket.BeginReceiveFrom(state_sync.Buffer, 0, 160, SocketFlags.None, ref endpoint, HandleReceiveFrom, state_sync);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new NtpException($"Something happened while trying to begin the receive operation : {message}");
            }
        }

        private void HandleReceiveFrom(IAsyncResult result)
        {
            int received = 0;
            try
            {
                received = socket.EndReceiveFrom(result, ref endpoint);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                throw new NtpException($"Something happened while trying to end the receive operation : {message}");
            }

            if (received != 160)
            {
                throw new NtpException($"Received buffer size doesn't match the required size : {received}");
            }

            var state_sync = (StateSync)result.AsyncState;

            var ntp_clock = Update(state_sync.Request, state_sync.Buffer, received);

            OnNtpReceived?.Invoke(ntp_clock);
        }
    }
}