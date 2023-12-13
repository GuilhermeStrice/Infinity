using System.Runtime.CompilerServices;
using System.Text;

namespace Infinity.Core
{
    public class MessageReader : IRecyclable
    {
        internal static readonly ObjectPool<MessageReader> ReaderPool = new ObjectPool<MessageReader>(() => new MessageReader());

        public byte[] ?Buffer;

        public int Length;
        public int Offset;

        public int BytesRemaining => Length - Position;

        internal int _position;
        internal int readHead;

        public int Position
        {
            get { return _position; }
            set
            {
                _position = value;
                readHead = value + Offset;
            }
        }

        #region Read Methods
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

            fixed (byte* bufPtr = &Buffer[readHead])
            {
                byte* outPtr = (byte*)&output;

                *outPtr = *bufPtr;
                *(outPtr + 1) = *(bufPtr + 1);
                *(outPtr + 2) = *(bufPtr + 2);
                *(outPtr + 3) = *(bufPtr + 3);
            }

            Position += 4;
            return output;
        }

        public string ReadString()
        {
            int len = ReadPackedInt32();

            if (BytesRemaining < len)
            {
                throw new InvalidDataException($"Read length is longer than message length: {len} of {BytesRemaining}");
            }

            string output = Encoding.UTF8.GetString(Buffer, readHead, len);

            Position += len;
            return output;
        }

        public byte[] ReadBytesAndSize()
        {
            int len = ReadPackedInt32();

            if (BytesRemaining < len)
            {
                throw new InvalidDataException($"Read length is longer than message length: {len} of {BytesRemaining}");
            }

            return ReadBytes(len);
        }

        public byte[] ReadBytes(int length)
        {
            if (BytesRemaining < length)
            {
                throw new InvalidDataException($"Read length is longer than message length: {length} of {BytesRemaining}");
            }

            byte[] output = new byte[length];
            Array.Copy(Buffer, readHead, output, 0, output.Length);
            Position += output.Length;

            return output;
        }

        ///
        public int ReadPackedInt32()
        {
            return (int)ReadPackedUInt32();
        }

        ///
        public uint ReadPackedUInt32()
        {
            bool readMore = true;
            int shift = 0;
            uint output = 0;

            while (readMore)
            {
                if (BytesRemaining < 1)
                {
                    throw new InvalidDataException($"Read length is longer than message length.");
                }

                byte b = ReadByte();

                if (b >= 0x80)
                {
                    readMore = true;
                    b ^= 0x80;
                }
                else
                {
                    readMore = false;
                }

                output |= (uint)(b << shift);
                shift += 7;
            }

            return output;
        }
        #endregion

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte FastByte()
        {
            _position++;
            return Buffer[readHead++];
        }

        public static MessageReader Get(byte[] buffer)
        {
            var output = ReaderPool.GetObject();

            output.Buffer = buffer;
            output.Offset = 0;
            output.Position = 0;
            output.Length = buffer.Length;

            return output;
        }

        public static MessageReader GetSized(int minSize)
        {
            var output = ReaderPool.GetObject();

            if (output.Buffer == null || output.Buffer.Length < minSize)
            {
                output.Buffer = new byte[minSize];
            }
            else
            {
                Array.Clear(output.Buffer, 0, output.Buffer.Length);
            }

            output.Offset = 0;
            output.Position = 0;
            return output;
        }

        public static MessageReader Get(MessageReader source)
        {
            var output = GetSized(source.Buffer.Length);
            System.Buffer.BlockCopy(source.Buffer, 0, output.Buffer, 0, source.Buffer.Length);

            output.Offset = source.Offset;

            output._position = source._position;
            output.readHead = source.readHead;

            output.Length = source.Length;

            return output;
        }

        public void Recycle()
        {
            ReaderPool.PutObject(this);
        }

        public MessageReader Duplicate()
        {
            var output = GetSized(Length);
            Array.Copy(Buffer, Offset, output.Buffer, 0, Length);
            output.Length = Length;
            output.Offset = 0;
            output.Position = 0;

            return output;
        }
    }
}
