using System.Buffers.Binary;

namespace Infinity.SNTP
{
    internal static class NtpDateTime
    {
        private const double FACTOR = 1L << 32;

        // SNTP epochs start every 2^32 seconds. The current one is in year 2036.
        // Calculations below will work for timestamps within +/- 68 years of the epoch,
        // which fortunately covers machines that just booted with time reset to unix epoch of 1970.
        // This code must be updated sometime after year 2070 as year 2104 approaches.
        public static readonly DateTime epoch = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc) + TimeSpan.FromSeconds(1L << 32);
        
        public static DateTime? Decode(long bits)
        {
            if (bits == 0)
            {
                return null;
            }

            return epoch + TimeSpan.FromSeconds(bits / FACTOR);
        }

        public static long Encode(DateTime? time)
        {
            if (time == null)
            {
                return 0;
            }

            return Convert.ToInt64((time.Value - epoch).TotalSeconds * (1L << 32));
        }
        public static DateTime? Read(ReadOnlySpan<byte> buffer)
        {
            return Decode(BinaryPrimitives.ReadInt64BigEndian(buffer));
        }

        public static void Write(Span<byte> buffer, DateTime? time)
        {
            BinaryPrimitives.WriteInt64BigEndian(buffer, Encode(time));
        }
    }
}