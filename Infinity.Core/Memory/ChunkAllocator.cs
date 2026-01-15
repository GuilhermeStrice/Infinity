using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Infinity.Core
{
    public unsafe class ChunkAllocator : IDisposable
    {
        private readonly int _slabSize;
        private readonly Stack<IntPtr> _freeSlabs = new Stack<IntPtr>();

        public ChunkAllocator(int slabSize)
        {
            if (slabSize <= 0) throw new ArgumentOutOfRangeException(nameof(slabSize));
            _slabSize = slabSize;
        }

        /// <summary>
        /// Allocates a block of memory. If size <= slabSize, reuses slab; otherwise allocates exact size.
        /// </summary>
        public byte* Allocate(int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));

            if (size <= _slabSize)
            {
                if (_freeSlabs.Count > 0)
                {
                    return (byte*)_freeSlabs.Pop();
                }
                return (byte*)Marshal.AllocHGlobal(_slabSize);
            }
            else
            {
                return (byte*)Marshal.AllocHGlobal(size);
            }
        }

        /// <summary>
        /// Frees a previously allocated block.
        /// </summary>
        /// <param name="ptr">Pointer to free.</param>
        /// <param name="size">Original allocation size. Determines if slab or large allocation.</param>
        public void Free(byte* ptr, int size = -1)
        {
            if (ptr == null) return;

            if (size <= _slabSize || size < 0)
            {
                // Return to slab pool
                _freeSlabs.Push((IntPtr)ptr);
            }
            else
            {
                // Free large allocation
                Marshal.FreeHGlobal((IntPtr)ptr);
            }
        }

        public void Dispose()
        {
            while (_freeSlabs.Count > 0)
            {
                Marshal.FreeHGlobal(_freeSlabs.Pop());
            }
        }
    }
}