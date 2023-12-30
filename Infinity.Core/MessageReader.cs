using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Infinity.Core
{
    public class MessageReader : IRecyclable
    {
        public byte[]? Buffer { get; set; }

        public int Length { get; set; }
        public int Offset { get; set; }

        public int BytesRemaining => Length - Position;

        internal int _position;
        internal int head;

        public int Position
        {
            get
            {
                return _position;
            }
            set
            {
                _position = value;
                head = value + Offset;
            }
        }

        internal MessageReader(int _buffer_size)
        {
            Buffer = new byte[_buffer_size];
        }

        public bool ReadBoolean()
        {
            byte val = FastByte();
            return val != 0;
        }

        public sbyte ReadSByte()
        {
            return (sbyte)FastByte();
        }

        public byte ReadByte()
        {
            return FastByte();
        }

        public ushort ReadUInt16()
        {
            ushort output =
                (ushort)(FastByte()
                | FastByte() << 8);
            return output;
        }

        public short ReadInt16()
        {
            short output =
                (short)(FastByte()
                | FastByte() << 8);
            return output;
        }

        public uint ReadUInt32()
        {
            uint output =
                FastByte()
                | (uint)FastByte() << 8
                | (uint)FastByte() << 16
                | (uint)FastByte() << 24;

            return output;
        }

        public int ReadInt32()
        {
            int output =
                FastByte()
                | FastByte() << 8
                | FastByte() << 16
                | FastByte() << 24;

            return output;
        }

        public ulong ReadUInt64()
        {
            ulong output =
                (ulong)FastByte()
                | (ulong)FastByte() << 8
                | (ulong)FastByte() << 16
                | (ulong)FastByte() << 24
                | (ulong)FastByte() << 32
                | (ulong)FastByte() << 40
                | (ulong)FastByte() << 48
                | (ulong)FastByte() << 56;

            return output;
        }

        public long ReadInt64()
        {
            long output =
                (long)FastByte()
                | (long)FastByte() << 8
                | (long)FastByte() << 16
                | (long)FastByte() << 24
                | (long)FastByte() << 32
                | (long)FastByte() << 40
                | (long)FastByte() << 48
                | (long)FastByte() << 56;

            return output;
        }

        public unsafe float ReadSingle()
        {
            float output = 0;

            fixed (byte* buf_ptr = &Buffer[head])
            {
                byte* out_ptr = (byte*)&output;

                *out_ptr = *buf_ptr;
                *(out_ptr + 1) = *(buf_ptr + 1);
                *(out_ptr + 2) = *(buf_ptr + 2);
                *(out_ptr + 3) = *(buf_ptr + 3);
            }

            Position += 4;
            return output;
        }

        public string ReadString()
        {
            int length = ReadPackedInt32();

            if (BytesRemaining < length)
            {
                throw new InvalidDataException($"Read length is longer than message length: {length} of {BytesRemaining}");
            }

            string output = Encoding.UTF8.GetString(Buffer, head, length);

            Position += length;
            return output;
        }

        public byte[] ReadBytesAndSize()
        {
            int length = ReadPackedInt32();

            if (BytesRemaining < length)
            {
                throw new InvalidDataException($"Read length is longer than message length: {length} of {BytesRemaining}");
            }

            return ReadBytes(length);
        }

        public byte[] ReadBytes(int _length)
        {
            if (BytesRemaining < _length)
            {
                throw new InvalidDataException($"Read length is longer than message length: {_length} of {BytesRemaining}");
            }

            byte[] output = new byte[_length];
            Array.Copy(Buffer, head, output, 0, output.Length);
            Position += output.Length;

            return output;
        }

        public int ReadPackedInt32()
        {
            return (int)ReadPackedUInt32();
        }

        public uint ReadPackedUInt32()
        {
            bool read_more = true;
            int shift = 0;
            uint output = 0;

            while (read_more)
            {
                if (BytesRemaining < 1)
                {
                    throw new InvalidDataException($"Read length is longer than message length.");
                }

                byte b = ReadByte();

                if (b >= 0x80)
                {
                    read_more = true;
                    b ^= 0x80;
                }
                else
                {
                    read_more = false;
                }

                output |= (uint)(b << shift);
                shift += 7;
            }

            return output;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FastByte()
        {
            _position++;
            return Buffer[head++];
        }

        public static MessageReader Get(byte[] _buffer, int _offset, int _length)
        {
            MessageReader reader = Pools.ReaderPool.GetObject();

            System.Buffer.BlockCopy(_buffer, _offset, reader.Buffer, 0, _length);

            reader.Offset = 0;
            reader.Position = 0;
            reader.Length = _length;

            return reader;
        }

        public static MessageReader Get(byte[] _buffer)
        {
            return Get(_buffer, 0, _buffer.Length);
        }

        public static MessageReader Get()
        {
            return Pools.ReaderPool.GetObject();
        }

        public void Recycle()
        {
            Pools.ReaderPool.PutObject(this);
        }

        public MessageReader Duplicate()
        {
            MessageReader reader = Get(Buffer, 0, Length);
            return reader;
        }
    }
}
