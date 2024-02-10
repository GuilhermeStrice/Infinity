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

        public event Action<NtpClock>? OnNtpReceived;
        public event Action<Exception>? OnInternalError;

        public NtpClock? Last { get; private set; }

        private EndPoint endpoint;
        private Socket socket;

        public NtpClient(EndPoint _endpoint, TimeSpan? _timeout = default)
        {
            endpoint = _endpoint;
            Timeout = _timeout ?? DefaultTimeout;

            socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
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

        public void Query()
        {
            var request = NtpRequest.Get();
            var ntp_packet = request.ToPacket();
            var buffer = ntp_packet.ToBytes();

            ntp_packet.Recycle();

            var state_sync = new StateSync();
            state_sync.Request = request;

            try
            {
                socket.BeginSendTo(buffer, 0, buffer.Length, SocketFlags.None, endpoint, HandleSendTo, state_sync);
            }
            catch (Exception ex)
            {
                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                OnInternalError?.Invoke(new NtpException($"Something happened while trying to begin the send operation : {message}"));
                return;
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
                OnInternalError?.Invoke(new NtpException($"Something happened while trying to end the send operation : {message}"));
                return;
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
                OnInternalError?.Invoke(new NtpException($"Something happened while trying to begin the receive operation : {message}"));
                return;
            }
        }

        private void HandleReceiveFrom(IAsyncResult result)
        {
            var state_sync = (StateSync)result.AsyncState;

            int received = 0;
            try
            {
                received = socket.EndReceiveFrom(result, ref endpoint);
            }
            catch (Exception ex)
            {
                state_sync.Request.Recycle();

                var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                OnInternalError?.Invoke(new NtpException($"Something happened while trying to end the receive operation : {message}"));
                return;
            }

            try
            {
                var ntp_clock = Update(state_sync.Request, state_sync.Buffer, received);
                state_sync.Request.Recycle();

                if (ntp_clock != null)
                {
                    OnNtpReceived?.Invoke(ntp_clock);
                }
                else
                {
                    state_sync.Request.Recycle();

                    OnInternalError?.Invoke(new NtpException($"Error : {received}"));
                }
            }
            catch
            {
                state_sync.Request.Recycle();

                OnInternalError?.Invoke(new NtpException($"Error : {received}"));
            }
        }

        private NtpClock? Update(NtpRequest _request, byte[] _buffer, int _length)
        {
            var ntp_packet = NtpPacket.FromBytes(_buffer, _length);
            var response = NtpResponse.FromPacket(ntp_packet);
            
            ntp_packet.Recycle();
            
            if (!response.Matches(_request))
            {
                OnInternalError?.Invoke(new NtpException("Response does not match the request."));
                return null;
            }

            var time = new NtpClock(response);
            if (time.Synchronized || Last == null)
            {
                Last = time;
            }

            return time;
        }
    }
}