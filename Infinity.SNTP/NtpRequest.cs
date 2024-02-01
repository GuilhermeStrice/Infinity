using Infinity.Core;

namespace Infinity.SNTP
{
    public class NtpRequest : IRecyclable
    {
        public DateTime TransmitTimestamp { get; set; } = DateTime.UtcNow;

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

            var ntp_request = Get();

            ntp_request.TransmitTimestamp = _packet.TransmitTimestamp.Value;

            return ntp_request;
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
            var tmp = ToPacket();
            tmp.Recycle();
        }

        public static NtpRequest Get()
        {
            var ntp_request = Pools.NtpRequestPool.GetObject();
            ntp_request.TransmitTimestamp = DateTime.UtcNow;

            return ntp_request;
        }

        public void Recycle()
        {
            Pools.NtpRequestPool.PutObject(this);
        }
    }
}