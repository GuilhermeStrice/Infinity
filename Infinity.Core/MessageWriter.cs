using System.Runtime.CompilerServices;
using System.Text;

namespace Infinity.Core
{
    public abstract class MessageWriter : IRecyclable
    {
        public static int BufferSize = 64000;

        public byte[] Buffer;
        public int Length;
        public int Position;

        public MessageWriter(byte[] buffer)
        {
            Buffer = buffer;
            Length = Buffer.Length;
        }

        public MessageWriter(int bufferSize)
        {
            Buffer = new byte[bufferSize];
        }

        public abstract bool HasBytes(int expected);

        public abstract void Recycle();

        #region WriteMethods

        public void CopyFrom(MessageReader other)
        {
            int offset, length;
            if (other.Tag == byte.MaxValue)
            {
                offset = other.Offset;
                length = other.Length;
            }
            else
            {
                offset = other.Offset - 3;
                length = other.Length + 3;
            }

            System.Buffer.BlockCopy(other.Buffer, offset, Buffer, Position, length);
            Position += length;
            if (Position > Length) Length = Position;
        }

        public void Write(bool value)
        {
            Buffer[Position++] = (byte)(value ? 1 : 0);
            if (Position > Length) Length = Position;
        }

        public void Write(sbyte value)
        {
            Buffer[Position++] = (byte)value;
            if (Position > Length) Length = Position;
        }

        public void Write(byte value)
        {
            Buffer[Position++] = value;
            if (Position > Length) Length = Position;
        }

        public void Write(short value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            if (Position > Length) Length = Position;
        }

        public void Write(ushort value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            if (Position > Length) Length = Position;
        }

        public void Write(uint value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            Buffer[Position++] = (byte)(value >> 16);
            Buffer[Position++] = (byte)(value >> 24);
            if (Position > Length) Length = Position;
        }

        public void Write(int value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            Buffer[Position++] = (byte)(value >> 16);
            Buffer[Position++] = (byte)(value >> 24);
            if (Position > Length) Length = Position;
        }

        public void Write(ulong value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            Buffer[Position++] = (byte)(value >> 16);
            Buffer[Position++] = (byte)(value >> 24);
            Buffer[Position++] = (byte)(value >> 32);
            Buffer[Position++] = (byte)(value >> 40);
            Buffer[Position++] = (byte)(value >> 48);
            Buffer[Position++] = (byte)(value >> 56);
            if (Position > Length) Length = Position;
        }

        public void Write(long value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            Buffer[Position++] = (byte)(value >> 16);
            Buffer[Position++] = (byte)(value >> 24);
            Buffer[Position++] = (byte)(value >> 32);
            Buffer[Position++] = (byte)(value >> 40);
            Buffer[Position++] = (byte)(value >> 48);
            Buffer[Position++] = (byte)(value >> 56);
            if (Position > Length) Length = Position;
        }

        public unsafe void Write(float value)
        {
            fixed (byte* ptr = &Buffer[Position])
            {
                byte* valuePtr = (byte*)&value;

                *ptr = *valuePtr;
                *(ptr + 1) = *(valuePtr + 1);
                *(ptr + 2) = *(valuePtr + 2);
                *(ptr + 3) = *(valuePtr + 3);
            }

            Position += 4;
            if (Position > Length) Length = Position;
        }

        public void Write(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            WritePacked(bytes.Length);
            Write(bytes);
        }

        public void WriteBytesAndSize(byte[] bytes)
        {
            WritePacked((uint)bytes.Length);
            Write(bytes);
        }

        public void WriteBytesAndSize(byte[] bytes, int length)
        {
            WritePacked((uint)length);
            Write(bytes, length);
        }

        public void WriteBytesAndSize(byte[] bytes, int offset, int length)
        {
            WritePacked((uint)length);
            Write(bytes, offset, length);
        }

        public void Write(byte[] bytes)
        {
            Array.Copy(bytes, 0, Buffer, Position, bytes.Length);
            Position += bytes.Length;
            if (Position > Length) Length = Position;
        }

        public void Write(byte[] bytes, int offset, int length)
        {
            Array.Copy(bytes, offset, Buffer, Position, length);
            Position += length;
            if (Position > Length) Length = Position;
        }

        public void Write(byte[] bytes, int length)
        {
            Array.Copy(bytes, 0, Buffer, Position, length);
            Position += length;
            if (Position > Length) Length = Position;
        }

        ///
        public void WritePacked(int value)
        {
            WritePacked((uint)value);
        }

        ///
        public void WritePacked(uint value)
        {
            do
            {
                byte b = (byte)(value & 0xFF);
                if (value >= 0x80)
                {
                    b |= 0x80;
                }

                Write(b);
                value >>= 7;
            } while (value > 0);
        }
        #endregion

        public unsafe static bool IsLittleEndian()
        {
            byte b;
            unsafe
            {
                int i = 1;
                byte* bp = (byte*)&i;
                b = *bp;
            }

            return b == 1;
        }
    }
}
