namespace Infinity.Core
{
    public unsafe class MemoryChunk : IDisposable
    {
        private byte* _ptr;
        private int _capacity;
        private int _length;
        private readonly ChunkAllocator _allocator;

        public byte* Ptr => _ptr;
        public int Length => _length;
        public int Capacity => _capacity;

        public MemoryChunk(ChunkAllocator allocator, int initialCapacity = 1024)
        {
            _allocator = allocator;
            _capacity = initialCapacity;
            _ptr = _allocator.Allocate(_capacity);
            _length = 0;
        }

        /// <summary>
        /// Ensures buffer has space for additional bytes. Grows automatically if needed.
        /// </summary>
        public void EnsureCapacity(int additional)
        {
            int required = _length + additional;
            if (required <= _capacity) return;

            int newCapacity = _capacity;
            while (newCapacity < required) newCapacity *= 2;

            byte* newPtr = _allocator.Allocate(newCapacity);
            Buffer.MemoryCopy(_ptr, newPtr, newCapacity, _length);
            _allocator.Free(_ptr, _capacity);

            _ptr = newPtr;
            _capacity = newCapacity;
        }

        /// <summary>
        /// Increases the length after writing bytes.
        /// </summary>
        public void Advance(int count)
        {
            _length += count;
        }

        public void SetLength(int length)
        {
            if (length < 0 || length > _capacity)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be between 0 and Capacity.");
            _length = length;
        }

        public void Dispose()
        {
            Free();
            GC.SuppressFinalize(this); // prevent finalizer from running twice
        }

        private void Free()
        {
            if (_ptr != null)
            {
                _allocator.Free(_ptr);
                _ptr = null;
                _capacity = 0;
                _length = 0;
            }
        }

        ~MemoryChunk()
        {
            // This will run if Dispose was not called
            Free();
        }

        /// <summary>
        /// Returns a read-only span of written bytes.
        /// </summary>
        public ReadOnlySpan<byte> AsSpan => new ReadOnlySpan<byte>(_ptr, _length);
    }
}