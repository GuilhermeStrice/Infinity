using System;
using System.IO;
using System.Text;
using Infinity.Core.Exceptions;

public unsafe struct MessageReader
{
    private BufferChunk _chunk;
    public int Position;

    private int _manualLength; // backing field for manual override

    public Span<byte> Buffer => new Span<byte>(_chunk.Ptr, Length);

    public int Length
    {
        get => _manualLength > 0 ? _manualLength : _chunk.Length;
        set
        {
            if (value < 0 || value > _chunk.Capacity)  // <-- use Capacity here, not Length
                throw new ArgumentOutOfRangeException(nameof(value), "Length must be within 0..chunk capacity.");

            _manualLength = value;
            _chunk.SetLength(value);

            if (Position > _manualLength)
                Position = _manualLength;
        }
    }

    public int BytesRemaining => _chunk.Length - Position;

    public MessageReader()
    {
        throw new InfinityException("MessageReader must be created with a BufferChunk or ChunkedByteAllocator");
    }

    public MessageReader(BufferChunk chunk)
    {
        _chunk = chunk;
        Position = 0;
    }

    public MessageReader(ChunkedByteAllocator allocator)
    {
        _chunk = new BufferChunk(allocator);
        Position = 0;
    }

    public MessageReader(ChunkedByteAllocator allocator, byte[] buffer, int offset, int length)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (offset < 0 || length < 0 || offset + length > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "Offset and length must be within buffer bounds.");

        _chunk = new BufferChunk(allocator, length);

        fixed (byte* src = &buffer[offset])
        {
            System.Buffer.MemoryCopy(src, _chunk.Ptr, _chunk.Capacity, length);
        }

        _chunk.SetLength(length);
        Position = 0;
        _manualLength = 0;
    }

    public byte this[int i]
    {
        get
        {
            if (i < 0 || i >= Length)
                throw new IndexOutOfRangeException();
            return _chunk.Ptr[i];
        }
        set => throw new InvalidOperationException("MessageReader is read-only.");
    }

    #region ReadMethods

    private byte FastByte()
    {
        if (Position >= _chunk.Length)
            throw new InvalidDataException("Attempted to read past message length.");
        return _chunk.Ptr[Position++];
    }

    public bool ReadBoolean() => FastByte() != 0;

    public sbyte ReadSByte() => (sbyte)FastByte();

    public byte ReadByte() => FastByte();

    public ushort ReadUInt16()
    {
        ushort output = (ushort)(FastByte() | FastByte() << 8);
        return output;
    }

    public short ReadInt16()
    {
        short output = (short)(FastByte() | FastByte() << 8);
        return output;
    }

    public uint ReadUInt32()
    {
        uint output = (uint)(FastByte()
            | FastByte() << 8
            | FastByte() << 16
            | FastByte() << 24);
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
        ulong output = 0;
        for (int i = 0; i < 8; i++)
            output |= (ulong)FastByte() << (i * 8);
        return output;
    }

    public long ReadInt64()
    {
        long output = 0;
        for (int i = 0; i < 8; i++)
            output |= (long)FastByte() << (i * 8);
        return output;
    }

    public float ReadSingle()
    {
        if (BytesRemaining < 4)
            throw new InvalidDataException("Not enough bytes to read float.");

        byte* ptr = _chunk.Ptr;
        float output = *(float*)(ptr + Position);
        Position += 4;
        return output;
    }


    public string ReadString()
    {
        int length = ReadPackedInt32();
        if (BytesRemaining < length)
            throw new InvalidDataException($"Read length {length} exceeds remaining bytes {BytesRemaining}.");

        string output = Encoding.UTF8.GetString(_chunk.Ptr + Position, length);
        Position += length;
        return output;
    }

    public byte[] ReadBytesAndSize()
    {
        int length = ReadPackedInt32();
        return ReadBytes(length);
    }

    public byte[] ReadBytes(int length)
    {
        if (BytesRemaining < length)
            throw new InvalidDataException($"Read length {length} exceeds remaining bytes {BytesRemaining}.");

        byte[] output = new byte[length];

        // Use System.Buffer.MemoryCopy with pointers
        fixed (byte* dst = output)
        {
            System.Buffer.MemoryCopy(_chunk.Ptr + Position, dst, length, length);
        }

        Position += length;
        return output;
    }

    public int ReadPackedInt32() => (int)ReadPackedUInt32();

    public uint ReadPackedUInt32()
    {
        uint output = 0;
        int shift = 0;

        while (true)
        {
            byte b = FastByte();
            output |= (uint)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
            if (shift > 35) throw new InvalidDataException("Packed uint32 is too large.");
        }

        return output;
    }

    #endregion

    public ReadOnlySpan<byte> ReadSpan(int length)
    {
        if (BytesRemaining < length)
            throw new InvalidDataException($"ReadSpan length {length} exceeds remaining bytes {BytesRemaining}.");

        var span = new ReadOnlySpan<byte>(_chunk.Ptr + Position, length);
        Position += length;
        return span;
    }

    public MessageWriter ToWriter()
    {
        return new MessageWriter(_chunk);
    }

    public unsafe UnmanagedMemoryManager AsManager()
    {
        var manager = new UnmanagedMemoryManager(_chunk.Ptr, _chunk.Capacity);
        return manager;
    }
}