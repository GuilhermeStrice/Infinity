using System.Buffers.Binary;

namespace Infinity.SNTP
{
    internal static class NtpTimeSpan
    {
        static TimeSpan Decode(int _bits)
        {
            return TimeSpan.FromSeconds(_bits / (double)(1 << 16));
        }

        static int Encode(TimeSpan _time)
        {
            return (int)(_time.TotalSeconds * (1 << 16));
        }

        public static TimeSpan Read(ReadOnlySpan<byte> _buffer)
        {
            return Decode(BinaryPrimitives.ReadInt32BigEndian(_buffer));
        }

        public static void Write(Span<byte> _buffer, TimeSpan _time)
        {
            BinaryPrimitives.WriteInt32BigEndian(_buffer, Encode(_time));
        }
    }
}