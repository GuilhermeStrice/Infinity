using System.Buffers.Binary;

namespace Infinity.SNTP
{
    public class NtpPacket
    {
        public NtpLeapIndicator LeapIndicator { get; init; } = NtpLeapIndicator.NoWarning;

        public int VersionNumber { get; init; } = 4;

        public NtpMode Mode { get; init; } = NtpMode.Client;

        public int Stratum { get; init; } = 0;

        public int PollInterval { get; init; } = 0;

        public int Precision { get; init; } = 0;

        public TimeSpan RootDelay { get; init; } = TimeSpan.Zero;

        public TimeSpan RootDispersion { get; init; } = TimeSpan.Zero;

        public uint ReferenceId { get; init; } = 0;

        public DateTime? ReferenceTimestamp { get; init; } = null;

        public DateTime? OriginTimestamp { get; init; } = null;

        public DateTime? ReceiveTimestamp { get; init; } = null;

        public DateTime? TransmitTimestamp { get; init; } = DateTime.UtcNow;

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
            var packet = new NtpPacket
            {
                LeapIndicator = (NtpLeapIndicator)((buffer[0] & 0xC0) >> 6),
                VersionNumber = (buffer[0] & 0x38) >> 3,
                Mode = (NtpMode)(buffer[0] & 0x07),
                Stratum = buffer[1],
                PollInterval = buffer[2],
                Precision = (sbyte)buffer[3],
                RootDelay = NtpTimeSpan.Read(bytes[4..]),
                RootDispersion = NtpTimeSpan.Read(bytes[8..]),
                ReferenceId = BinaryPrimitives.ReadUInt32BigEndian(bytes[12..]),
                ReferenceTimestamp = NtpDateTime.Read(bytes[16..]),
                OriginTimestamp = NtpDateTime.Read(bytes[24..]),
                ReceiveTimestamp = NtpDateTime.Read(bytes[32..]),
                TransmitTimestamp = NtpDateTime.Read(bytes[40..]),
            };
            packet.Validate();
            return packet;
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
    }
}