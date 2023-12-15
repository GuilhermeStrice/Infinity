using System.Text;

namespace Infinity.Core
{
    public class MessageWriter : IRecyclable
    {
        public static int BufferSize = 64000;

        public byte[] Buffer;
        public int Length;
        public int Position;

        internal MessageWriter(int _buffer_size)
        {
            Buffer = new byte[_buffer_size];
        }

        #region WriteMethods
        private void FixLength()
        {
            if (Position > Length)
            {
                Length = Position;
            }
        }

        public void Write(bool _value)
        {
            Buffer[Position++] = (byte)(_value ? 1 : 0);
            FixLength();
        }

        public void Write(sbyte _value)
        {
            Buffer[Position++] = (byte)_value;
            FixLength();
        }

        public void Write(byte _value)
        {
            Buffer[Position++] = _value;
            FixLength();
        }

        public void Write(short _value)
        {
            Buffer[Position++] = (byte)_value;
            Buffer[Position++] = (byte)(_value >> 8);
            FixLength();
        }

        public void Write(ushort _value)
        {
            Buffer[Position++] = (byte)_value;
            Buffer[Position++] = (byte)(_value >> 8);
            FixLength();
        }

        public void Write(uint _value)
        {
            Buffer[Position++] = (byte)_value;
            Buffer[Position++] = (byte)(_value >> 8);
            Buffer[Position++] = (byte)(_value >> 16);
            Buffer[Position++] = (byte)(_value >> 24);
            FixLength();
        }

        public void Write(int _value)
        {
            Buffer[Position++] = (byte)_value;
            Buffer[Position++] = (byte)(_value >> 8);
            Buffer[Position++] = (byte)(_value >> 16);
            Buffer[Position++] = (byte)(_value >> 24);
            FixLength();
        }

        public void Write(ulong _value)
        {
            Buffer[Position++] = (byte)_value;
            Buffer[Position++] = (byte)(_value >> 8);
            Buffer[Position++] = (byte)(_value >> 16);
            Buffer[Position++] = (byte)(_value >> 24);
            Buffer[Position++] = (byte)(_value >> 32);
            Buffer[Position++] = (byte)(_value >> 40);
            Buffer[Position++] = (byte)(_value >> 48);
            Buffer[Position++] = (byte)(_value >> 56);
            FixLength();
        }

        public void Write(long _value)
        {
            Buffer[Position++] = (byte)_value;
            Buffer[Position++] = (byte)(_value >> 8);
            Buffer[Position++] = (byte)(_value >> 16);
            Buffer[Position++] = (byte)(_value >> 24);
            Buffer[Position++] = (byte)(_value >> 32);
            Buffer[Position++] = (byte)(_value >> 40);
            Buffer[Position++] = (byte)(_value >> 48);
            Buffer[Position++] = (byte)(_value >> 56);
            FixLength();
        }

        public unsafe void Write(float _value)
        {
            fixed (byte* buffer_ptr = &Buffer[Position])
            {
                byte* value_ptr = (byte*)&_value;

                *buffer_ptr = *value_ptr;
                *(buffer_ptr + 1) = *(value_ptr + 1);
                *(buffer_ptr + 2) = *(value_ptr + 2);
                *(buffer_ptr + 3) = *(value_ptr + 3);
            }

            Position += 4;
            FixLength();
        }

        public void Write(string _value)
        {
            var bytes = Encoding.UTF8.GetBytes(_value);
            WritePacked(bytes.Length);
            Write(bytes);
        }

        public void WriteBytesAndSize(byte[] _bytes)
        {
            WritePacked((uint)_bytes.Length);
            Write(_bytes);
        }

        public void WriteBytesAndSize(byte[] _bytes, int _length)
        {
            WritePacked((uint)_length);
            Write(_bytes, _length);
        }

        public void WriteBytesAndSize(byte[] _bytes, int _offset, int _length)
        {
            WritePacked((uint)_length);
            Write(_bytes, _offset, _length);
        }

        public void Write(byte[] _bytes)
        {
            Array.Copy(_bytes, 0, Buffer, Position, _bytes.Length);
            Position += _bytes.Length;
            FixLength();
        }

        public void Write(byte[] _bytes, int _offset, int _length)
        {
            Array.Copy(_bytes, _offset, Buffer, Position, _length);
            Position += _length;
            FixLength();
        }

        public void Write(byte[] _bytes, int _length)
        {
            Array.Copy(_bytes, 0, Buffer, Position, _length);
            Position += _length;
            FixLength();
        }

        public void WritePacked(int _value)
        {
            WritePacked((uint)_value);
        }

        public void WritePacked(uint _value)
        {
            do
            {
                byte b = (byte)(_value & 0xFF);
                if (_value >= 0x80)
                {
                    b |= 0x80;
                }

                Write(b);
                _value >>= 7;
            }
            while (_value > 0);
        }
        #endregion

        public void Write(MessageWriter _msg, int _offset)
        {
            Write(_msg.Buffer, _offset, _msg.Length - _offset);
        }

        public byte[] ToByteArray(int _offset)
        {
            byte[] output = new byte[Length - _offset];
            System.Buffer.BlockCopy(Buffer, _offset, output, 0, Length - _offset);
            return output;
        }

        public static MessageWriter Get()
        {
            var output = Pools.WriterPool.GetObject();

            Array.Clear(output.Buffer, 0, output.Buffer.Length);
            output.Length = output.Position = 0;

            return output;
        }

        public void Recycle()
        {
            Pools.WriterPool.PutObject(this);
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
