using System.Buffers;
using System.Runtime.InteropServices;

namespace Infinity.Core
{
    public unsafe class UnmanagedMemoryManager : MemoryManager<byte>
    {
        private readonly byte* _ptr;
        private readonly int _length;

        public UnmanagedMemoryManager(byte* ptr, int length)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            _ptr = ptr;
            _length = length;
        }

        public override Span<byte> GetSpan() => new Span<byte>(_ptr, _length);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex > _length)
                throw new ArgumentOutOfRangeException(nameof(elementIndex));

            // No GCHandle needed because memory is unmanaged
            return new MemoryHandle(_ptr + elementIndex);
        }

        public override void Unpin()
        {
            // Nothing to do for unmanaged memory
        }

        protected override void Dispose(bool disposing)
        {
            // Nothing to free: memory is externally owned
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}