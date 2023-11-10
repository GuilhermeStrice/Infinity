using System.Runtime.CompilerServices;
using System.Text;

namespace Infinity.Core
{
    public abstract class MessageReader : IRecyclable
    {
        public byte[] ?Buffer;
        public byte Tag;

        public int Length;
        public int Offset;

        public int BytesRemaining => Length - Position;

        protected MessageReader ?Parent;

        public int Position
        {
            get { return _position; }
            set
            {
                _position = value;
                readHead = value + Offset;
            }
        }

        protected int _position;
        protected int readHead;

        /// <summary>
        /// Produces a MessageReader using the parent's buffer. This MessageReader should **NOT** be recycled.
        /// </summary>
        public abstract MessageReader ReadMessage();

        /// <summary>
        /// Produces a MessageReader with a new buffer. This MessageReader should be recycled.
        /// </summary>
        public abstract MessageReader ReadMessageAsNewBuffer();

        public abstract MessageReader Duplicate();

        public abstract void RemoveMessage(MessageReader reader);

        public abstract void InsertMessage(MessageReader reader, MessageWriter writer);

        protected abstract void AdjustLength(int offset, int amount);

        public abstract void Recycle();

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
            uint output = FastByte()
                | (uint)FastByte() << 8
                | (uint)FastByte() << 16
                | (uint)FastByte() << 24;

            return output;
        }

        public int ReadInt32()
        {
            int output = FastByte()
                | FastByte() << 8
                | FastByte() << 16
                | FastByte() << 24;

            return output;
        }

        public ulong ReadUInt64()
        {
            ulong output = (ulong)FastByte()
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
            long output = (long)FastByte()
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
            if (BytesRemaining < len) throw new InvalidDataException($"Read length is longer than message length: {len} of {BytesRemaining}");

            string output = Encoding.UTF8.GetString(Buffer, readHead, len);

            Position += len;
            return output;
        }

        public byte[] ReadBytesAndSize()
        {
            int len = ReadPackedInt32();
            if (BytesRemaining < len) throw new InvalidDataException($"Read length is longer than message length: {len} of {BytesRemaining}");

            return ReadBytes(len);
        }

        public byte[] ReadBytes(int length)
        {
            if (BytesRemaining < length) throw new InvalidDataException($"Read length is longer than message length: {length} of {BytesRemaining}");

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
                if (BytesRemaining < 1) throw new InvalidDataException($"Read length is longer than message length.");

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
