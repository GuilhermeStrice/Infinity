using System.Text;

namespace Infinity.Core
{
    public class MessageWriter : IRecyclable
    {
        internal static readonly ObjectPool<MessageWriter> WriterPool = new ObjectPool<MessageWriter>(() => new MessageWriter(BufferSize));

        public static int BufferSize = 64000;

        public byte[] Buffer;
        public int Length;
        public int Position;

        public byte SendOption { get; private set; }

        internal MessageWriter(byte[] buffer)
        {
            Buffer = buffer;
            Length = Buffer.Length;
        }

        internal MessageWriter(int bufferSize)
        {
            Buffer = new byte[bufferSize];
        }

        #region WriteMethods
        private void FixLength()
        {
            if (Position > Length)
            {
                Length = Position;
            }
        }

        public void Write(bool value)
        {
            Buffer[Position++] = (byte)(value ? 1 : 0);
            FixLength();
        }

        public void Write(sbyte value)
        {
            Buffer[Position++] = (byte)value;
            FixLength();
        }

        public void Write(byte value)
        {
            Buffer[Position++] = value;
            FixLength();
        }

        public void Write(short value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            FixLength();
        }

        public void Write(ushort value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            FixLength();
        }

        public void Write(uint value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            Buffer[Position++] = (byte)(value >> 16);
            Buffer[Position++] = (byte)(value >> 24);
            FixLength();
        }

        public void Write(int value)
        {
            Buffer[Position++] = (byte)value;
            Buffer[Position++] = (byte)(value >> 8);
            Buffer[Position++] = (byte)(value >> 16);
            Buffer[Position++] = (byte)(value >> 24);
            FixLength();
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
            FixLength();
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
            FixLength();
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
            FixLength();
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
            FixLength();
        }

        public void Write(byte[] bytes, int offset, int length)
        {
            Array.Copy(bytes, offset, Buffer, Position, length);
            Position += length;
            FixLength();
        }

        public void Write(byte[] bytes, int length)
        {
            Array.Copy(bytes, 0, Buffer, Position, length);
            Position += length;
            FixLength();
        }

        public void WritePacked(int value)
        {
            WritePacked((uint)value);
        }

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
            }
            while (value > 0);
        }
        #endregion

        public void Write(MessageWriter msg, int _offset)
        {
            Write(msg.Buffer, _offset, msg.Length - _offset);
        }

        public byte[] ToByteArray(int _offset)
        {
            byte[] output = new byte[Length - _offset];
            System.Buffer.BlockCopy(Buffer, _offset, output, 0, Length - _offset);
            return output;
        }

        public static MessageWriter Get(byte sendOption, int _offset)
        {
            var output = WriterPool.GetObject();
            output.Clear(sendOption, _offset);

            return output;
        }

        public void Clear(byte sendOption, int _offset)
        {
            Array.Clear(Buffer, 0, Buffer.Length);

            Buffer[0] = SendOption = sendOption;
            Length = Position = _offset;
        }

        public void Recycle()
        {
            Position = Length = 0;
            WriterPool.PutObject(this);
        }

        public MessageReader AsReader()
        {
            var reader = MessageReader.GetSized(Length);

            Array.Copy(Buffer, 0, reader.Buffer, 0, Length);
            reader.Length = Length;

            return reader;
        }
    }
}
