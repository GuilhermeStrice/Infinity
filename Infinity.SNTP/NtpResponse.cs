namespace Infinity.SNTP
{
    public class NtpResponse
    {
        public NtpLeapIndicator LeapIndicator { get; init; } = NtpLeapIndicator.NoWarning;

        public int Stratum { get; init; } = 0;

        public int PollInterval { get; init; } = 0;

        public int Precision { get; init; } = 0;

        public TimeSpan RootDelay { get; init; } = TimeSpan.Zero;

        public TimeSpan RootDispersion { get; init; } = TimeSpan.Zero;

        public uint ReferenceId { get; init; } = 0;

        public DateTime? ReferenceTimestamp { get; init; } = null;

        public DateTime OriginTimestamp { get; init; }

        public DateTime ReceiveTimestamp { get; init; }

        public DateTime TransmitTimestamp { get; init; }

        public DateTime DestinationTimestamp { get; init; }

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

            return new NtpResponse
            {
                LeapIndicator = _packet.LeapIndicator,
                Stratum = _packet.Stratum,
                PollInterval = _packet.PollInterval,
                Precision = _packet.Precision,
                RootDelay = _packet.RootDelay,
                RootDispersion = _packet.RootDispersion,
                ReferenceId = _packet.ReferenceId,
                ReferenceTimestamp = _packet.ReferenceTimestamp,
                OriginTimestamp = _packet.OriginTimestamp.Value,
                ReceiveTimestamp = _packet.ReceiveTimestamp.Value,
                TransmitTimestamp = _packet.TransmitTimestamp.Value,
                DestinationTimestamp = _time,
            };
        }

        public static NtpResponse FromPacket(NtpPacket _packet)
        {
            return FromPacket(_packet, DateTime.UtcNow);
        }

        public NtpPacket ToPacket()
        {
            var packet = new NtpPacket
            {
                Mode = NtpMode.Server,
                LeapIndicator = LeapIndicator,
                Stratum = Stratum,
                PollInterval = PollInterval,
                Precision = Precision,
                RootDelay = RootDelay,
                RootDispersion = RootDispersion,
                ReferenceId = ReferenceId,
                ReferenceTimestamp = ReferenceTimestamp,
                OriginTimestamp = OriginTimestamp,
                ReceiveTimestamp = ReceiveTimestamp,
                TransmitTimestamp = TransmitTimestamp,
            };

            packet.Validate();

            return packet;
        }

        public void Validate()
        {
            ToPacket();
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
    }
}