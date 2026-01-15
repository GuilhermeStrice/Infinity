using System;
using System.Runtime.CompilerServices;
using System.Text;
using Infinity.Core.Exceptions;

namespace Infinity.Core
{
    public unsafe struct MessageWriter
    {
        private MemoryChunk _chunk;
        public int Position;

        public int Length
        {
            get
            {
                return _chunk.Length;
            }

            set
            {
                if (value < 0 || value > _chunk.Capacity)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (Position > _chunk.Length)
                    _chunk.SetLength(Position);
            }
        }

        public Span<byte> Buffer => new Span<byte>(_chunk.Ptr, _chunk.Length);

        public MessageWriter()
        {
            throw new InfinityException("MessageWriter must be created with a MemoryChunk or ChunkAllocator");
        }

        public MessageWriter(MemoryChunk chunk)
        {
            _chunk = chunk;
            Position = 0;
        }

        public MessageWriter(ChunkAllocator allocator)
        {
            _chunk = new MemoryChunk(allocator);
            Position = 0;
        }

        public byte this[int i]
        {
            get
            {
                if (i < 0 || i >= Position)
                    throw new IndexOutOfRangeException();
                return _chunk.Ptr[i];
            }
            set
            {
                if (i < 0)
                    throw new IndexOutOfRangeException();

                // Automatically grow if writing past current Position
                if (i >= _chunk.Capacity)
                {
                    _chunk.EnsureCapacity(i - _chunk.Length + 1);
                }

                _chunk.Ptr[i] = value;

                // Update Position and Length if we wrote past current Position
                if (i >= Position) Position = i + 1;
                FixLength();
            }
        }

        /// <summary>
        /// Ensures there is enough space in the chunk, grows if necessary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Ensure(int count)
        {
            int required = Position + count;
            if (required > _chunk.Capacity)
            {
                _chunk.EnsureCapacity(required - _chunk.Length);
            }
        }

        private void FixLength()
        {
            if (Position > _chunk.Length)
            {
                _chunk.Advance(Position - _chunk.Length);
            }
        }

        public void Write(bool value)
        {
            Ensure(1);
            _chunk.Ptr[Position++] = (byte)(value ? 1 : 0);
            FixLength();
        }

        public void Write(byte value)
        {
            Ensure(1);
            _chunk.Ptr[Position++] = value;
            FixLength();
        }

        public void Write(short value)
        {
            Ensure(2);
            byte* p = _chunk.Ptr + Position;
            p[0] = (byte)value;
            p[1] = (byte)(value >> 8);
            Position += 2;
            FixLength();
        }

        public void Write(int value)
        {
            Ensure(4);
            byte* p = _chunk.Ptr + Position;
            p[0] = (byte)value;
            p[1] = (byte)(value >> 8);
            p[2] = (byte)(value >> 16);
            p[3] = (byte)(value >> 24);
            Position += 4;
            FixLength();
        }

        public void Write(long value)
        {
            Ensure(8);
            byte* p = _chunk.Ptr + Position;
            for (int i = 0; i < 8; i++)
                p[i] = (byte)(value >> (i * 8));
            Position += 8;
            FixLength();
        }

        public unsafe void Write(float value)
        {
            Ensure(4);
            Unsafe.WriteUnaligned(_chunk.Ptr + Position, value);
            Position += 4;
            FixLength();
        }

        public void Write(string value)
        {
            if (value == null) value = string.Empty;
            var bytes = Encoding.UTF8.GetBytes(value);
            WritePacked(bytes.Length);
            Write(bytes);
        }

        public void Write(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return;
            Ensure(bytes.Length);
            fixed (byte* src = bytes)
            {
                // Use the static System.Buffer.MemoryCopy method
                System.Buffer.MemoryCopy(src, _chunk.Ptr + Position, _chunk.Capacity - Position, bytes.Length);
            }
            Position += bytes.Length;
            FixLength();
        }

        public void Write(byte[] bytes, int offset, int length)
        {
            if (bytes == null || length == 0) return;
            Ensure(length);
            fixed (byte* src = &bytes[offset])
            {
                System.Buffer.MemoryCopy(src, _chunk.Ptr + Position, _chunk.Capacity - Position, length);
            }
            Position += length;
            FixLength();
        }

        public void Write(Span<byte> source, int offset, int length)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (offset < 0 || length < 0 || offset + length > source.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset and length must be within span bounds.");

            Ensure(length);

            // Copy the span slice into the chunk
            var slice = source.Slice(offset, length);
            for (int i = 0; i < length; i++)
            {
                _chunk.Ptr[Position + i] = slice[i];
            }

            Position += length;
            FixLength();
        }

        public void WritePacked(int value) => WritePacked((uint)value);

        public void WritePacked(uint value)
        {
            do
            {
                byte b = (byte)(value & 0x7F);
                value >>= 7;
                if (value != 0) b |= 0x80;
                Write(b);
            } while (value != 0);
        }

        public MessageReader ToReader()
        {
            // Create a reader over the same chunk
            // Position starts at 0, reads up to the current written length
            return new MessageReader(_chunk) { Position = 0 };
        }

        public unsafe UnmanagedMemoryManager AsManager()
        {
            var manager = new UnmanagedMemoryManager(_chunk.Ptr, _chunk.Length);
            return manager;
        }
    }
}