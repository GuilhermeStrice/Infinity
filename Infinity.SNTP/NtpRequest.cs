namespace Infinity.SNTP
{
    public class NtpRequest
    {
        public DateTime TransmitTimestamp { get; init; } = DateTime.UtcNow;

        public static NtpRequest FromPacket(NtpPacket _packet)
        {
            if (_packet.Mode != NtpMode.Client)
            {
                throw new NtpException("Not a request packet.");
            }

            if (_packet.TransmitTimestamp == null)
            {
                throw new NtpException("Request packet must have transit timestamp.");
            }

            return new NtpRequest { TransmitTimestamp = _packet.TransmitTimestamp.Value };
        }

        public NtpPacket ToPacket()
        {
            var packet = new NtpPacket
            {
                Mode = NtpMode.Client,
                TransmitTimestamp = TransmitTimestamp
            };

            packet.Validate();

            return packet;
        }

        public void Validate()
        {
            ToPacket();
        }
    }
}