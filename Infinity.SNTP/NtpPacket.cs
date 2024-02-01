using Infinity.Core;
using System.Buffers.Binary;

namespace Infinity.SNTP
{
    public class NtpPacket : IRecyclable
    {
        public NtpLeapIndicator LeapIndicator { get; set; } = NtpLeapIndicator.NoWarning;

        public int VersionNumber { get; set; } = 4;

        public NtpMode Mode { get; set; } = NtpMode.Client;

        public int Stratum { get; set; } = 0;

        public int PollInterval { get; set; } = 0;

        public int Precision { get; set; } = 0;

        public TimeSpan RootDelay { get; set; } = TimeSpan.Zero;

        public TimeSpan RootDispersion { get; set; } = TimeSpan.Zero;

        public uint ReferenceId { get; set; } = 0;

        public DateTime? ReferenceTimestamp { get; set; } = null;

        public DateTime? OriginTimestamp { get; set; } = null;

        public DateTime? ReceiveTimestamp { get; set; } = null;

        public DateTime? TransmitTimestamp { get; set; } = DateTime.UtcNow;

        public void Validate()
        {
            if (VersionNumber < 1 || VersionNumber > 7)
            {
                throw new NtpException("Invalid SNTP protocol version.");
            }

            if (!Enum.IsDefined(LeapIndicator))
            {
                throw new NtpException("Invalid leap second indicator value.");
            }

            if (!Enum.IsDefined(Mode))
            {
                throw new NtpException("Invalid NTP protocol mode.");
            }

            if ((byte)Stratum != Stratum)
            {
                throw new NtpException("Invalid stratum number.");
            }

            if ((byte)PollInterval != PollInterval)
            {
                throw new NtpException("Poll interval out of range.");
            }

            if ((sbyte)Precision != Precision)
            {
                throw new NtpException("Precision out of range.");
            }

            if (Math.Abs(RootDelay.TotalSeconds) > 32000)
            {
                throw new NtpException("Root delay out of range.");
            }

            if (RootDispersion.Ticks < 0 || RootDispersion.TotalSeconds > 32000)
            {
                throw new NtpException("Root dispersion out of range.");
            }

            if (ReferenceTimestamp != null && ReferenceTimestamp.Value.Kind != DateTimeKind.Utc)
            {
                throw new NtpException("Reference timestamp must have UTC timezone.");
            }

            if (OriginTimestamp != null && OriginTimestamp.Value.Kind != DateTimeKind.Utc)
            {
                throw new NtpException("Origin timestamp must have UTC timezone.");
            }

            if (ReceiveTimestamp != null && ReceiveTimestamp.Value.Kind != DateTimeKind.Utc)
            {
                throw new NtpException("Receive timestamp must have UTC timezone.");
            }

            if (TransmitTimestamp != null && TransmitTimestamp.Value.Kind != DateTimeKind.Utc)
            {
                throw new NtpException("Transmit timestamp must have UTC timezone.");
            }
        }

        public static NtpPacket FromBytes(byte[] buffer, int length)
        {
            if (length < 48 || length > buffer.Length)
            {
                throw new NtpException("NTP packet must be at least 48 bytes long.");
            }

            var bytes = buffer.AsSpan();
            var ntp_packet = Get();

            ntp_packet.LeapIndicator = (NtpLeapIndicator)((buffer[0] & 0xC0) >> 6);
            ntp_packet.VersionNumber = (buffer[0] & 0x38) >> 3;
            ntp_packet.Mode = (NtpMode)(buffer[0] & 0x07);
            ntp_packet.Stratum = buffer[1];
            ntp_packet.PollInterval = buffer[2];
            ntp_packet.Precision = (sbyte)buffer[3];
            ntp_packet.RootDelay = NtpTimeSpan.Read(bytes[4..]);
            ntp_packet.RootDispersion = NtpTimeSpan.Read(bytes[8..]);
            ntp_packet.ReferenceId = BinaryPrimitives.ReadUInt32BigEndian(bytes[12..]);
            ntp_packet.ReferenceTimestamp = NtpDateTime.Read(bytes[16..]);
            ntp_packet.OriginTimestamp = NtpDateTime.Read(bytes[24..]);
            ntp_packet.ReceiveTimestamp = NtpDateTime.Read(bytes[32..]);
            ntp_packet.TransmitTimestamp = NtpDateTime.Read(bytes[40..]);

            ntp_packet.Validate();
            return ntp_packet;
        }

        public byte[] ToBytes()
        {
            Validate();
            var buffer = new byte[48];
            var bytes = buffer.AsSpan();
            bytes[0] = (byte)(((uint)LeapIndicator << 6) | ((uint)VersionNumber << 3) | (uint)Mode);
            bytes[1] = (byte)Stratum;
            bytes[2] = (byte)PollInterval;
            bytes[3] = (byte)Precision;
            NtpTimeSpan.Write(bytes[4..], RootDelay);
            NtpTimeSpan.Write(bytes[8..], RootDispersion);
            BinaryPrimitives.WriteUInt32BigEndian(bytes[12..], ReferenceId);
            NtpDateTime.Write(bytes[16..], ReferenceTimestamp);
            NtpDateTime.Write(bytes[24..], OriginTimestamp);
            NtpDateTime.Write(bytes[32..], ReceiveTimestamp);
            NtpDateTime.Write(bytes[40..], TransmitTimestamp);
            return buffer;
        }

        public static NtpPacket Get()
        {
            var ntp_packet = Pools.NtpPacketPool.GetObject();

            ntp_packet.LeapIndicator = NtpLeapIndicator.NoWarning;
            ntp_packet.VersionNumber = 4;
            ntp_packet.Mode = NtpMode.Client;
            ntp_packet.Stratum = 0;
            ntp_packet.PollInterval = 0;
            ntp_packet.Precision = 0;
            ntp_packet.RootDelay = TimeSpan.Zero;
            ntp_packet.RootDispersion = TimeSpan.Zero;
            ntp_packet.ReferenceId = 0;
            ntp_packet.ReferenceTimestamp = null;
            ntp_packet.OriginTimestamp = null;
            ntp_packet.ReceiveTimestamp = null;
            ntp_packet.TransmitTimestamp = DateTime.UtcNow;

            return ntp_packet;
        }

        public void Recycle()
        {
            Pools.NtpPacketPool.PutObject(this);
        }
    }
}