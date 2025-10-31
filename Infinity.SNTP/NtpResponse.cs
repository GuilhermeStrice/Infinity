using Infinity.Core;

namespace Infinity.SNTP
{
    public class NtpResponse : IRecyclable
    {
        public NtpLeapIndicator LeapIndicator { get; set; } = NtpLeapIndicator.NoWarning;

        public int Stratum { get; set; } = 0;

        public int PollInterval { get; set; } = 0;

        public int Precision { get; set; } = 0;

        public TimeSpan RootDelay { get; set; } = TimeSpan.Zero;

        public TimeSpan RootDispersion { get; set; } = TimeSpan.Zero;

        public uint ReferenceId { get; set; } = 0;

        public DateTime? ReferenceTimestamp { get; set; } = null;

        public DateTime OriginTimestamp { get; set; }

        public DateTime ReceiveTimestamp { get; set; }

        public DateTime TransmitTimestamp { get; set; }

        public DateTime DestinationTimestamp { get; set; }

        public static NtpResponse FromPacket(NtpPacket _packet, DateTime _time)
        {
            _packet.Validate();
            if (_packet.Mode != NtpMode.Server)
            {
                throw new NtpException("Not a response packet.");
            }

            if (_packet.OriginTimestamp == null)
            {
                throw new NtpException("Origin timestamp is missing.");
            }

            if (_packet.ReceiveTimestamp == null)
            {
                throw new NtpException("Receive timestamp is missing.");
            }

            if (_packet.TransmitTimestamp == null)
            {
                throw new NtpException("Transmit timestamp is missing.");
            }

            if (_time.Kind != DateTimeKind.Utc)
            {
                throw new NtpException("Destination timestamp must have UTC timezone.");
            }

            var ntp_response = Get();

            ntp_response.LeapIndicator = _packet.LeapIndicator;
            ntp_response.Stratum = _packet.Stratum;
            ntp_response.PollInterval = _packet.PollInterval;
            ntp_response.Precision = _packet.Precision;
            ntp_response.RootDelay = _packet.RootDelay;
            ntp_response.RootDispersion = _packet.RootDispersion;
            ntp_response.ReferenceId = _packet.ReferenceId;
            ntp_response.ReferenceTimestamp = _packet.ReferenceTimestamp;
            ntp_response.OriginTimestamp = _packet.OriginTimestamp.Value;
            ntp_response.ReceiveTimestamp = _packet.ReceiveTimestamp.Value;
            ntp_response.TransmitTimestamp = _packet.TransmitTimestamp.Value;
            ntp_response.DestinationTimestamp = _time;

            return ntp_response;
        }

        public static NtpResponse FromPacket(NtpPacket _packet)
        {
            return FromPacket(_packet, DateTime.UtcNow);
        }

        public NtpPacket ToPacket()
        {
            var ntp_packet = NtpPacket.Get();

            ntp_packet.Mode = NtpMode.Server;
            ntp_packet.LeapIndicator = LeapIndicator;
            ntp_packet.Stratum = Stratum;
            ntp_packet.PollInterval = PollInterval;
            ntp_packet.Precision = Precision;
            ntp_packet.RootDelay = RootDelay;
            ntp_packet.RootDispersion = RootDispersion;
            ntp_packet.ReferenceId = ReferenceId;
            ntp_packet.ReferenceTimestamp = ReferenceTimestamp;
            ntp_packet.OriginTimestamp = OriginTimestamp;
            ntp_packet.ReceiveTimestamp = ReceiveTimestamp;
            ntp_packet.TransmitTimestamp = TransmitTimestamp;

            ntp_packet.Validate();

            return ntp_packet;
        }

        public void Validate()
        {
            var tmp = ToPacket();
            tmp.Recycle();
            if (DestinationTimestamp.Kind != DateTimeKind.Utc)
            {
                throw new NtpException("Destination timestamp must have UTC timezone.");
            }
        }

        public bool Matches(NtpRequest _request)
        {
            // Tolerate rounding errors on both sides.
            return Math.Abs((OriginTimestamp - _request.TransmitTimestamp).TotalSeconds) < 0.000_001;
        }

        public static NtpResponse Get()
        {
            var ntp_response = Pools.NtpResponsePool.GetObject();

            ntp_response.LeapIndicator = NtpLeapIndicator.NoWarning;
            ntp_response.Stratum = 0;
            ntp_response.PollInterval = 0;
            ntp_response.Precision = 0;
            ntp_response.RootDelay = TimeSpan.Zero;
            ntp_response.RootDispersion = TimeSpan.Zero;
            ntp_response.ReferenceId = 0;
            ntp_response.ReferenceTimestamp = null;
            ntp_response.OriginTimestamp = default;
            ntp_response.ReceiveTimestamp = default;
            ntp_response.TransmitTimestamp = default;
            ntp_response.DestinationTimestamp = default;

            return ntp_response;
        }

        public void Recycle()
        {
            Pools.NtpResponsePool.PutObject(this);
        }
    }
}