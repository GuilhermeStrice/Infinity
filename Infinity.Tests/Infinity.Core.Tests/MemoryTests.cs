using System;
using System.Linq;
using System.Text;
using Xunit;
using Infinity.Core; // your namespace

public class MessageWriterReaderTests
{
    private ChunkedByteAllocator _allocator = new ChunkedByteAllocator(1024);

    [Fact]
    public void WriteRead_Primitives_ShouldMatch()
    {
        var writer = new MessageWriter(_allocator);
        writer.Write(true);
        writer.Write(false);
        writer.Write((byte)123);
        writer.Write((short)32000);
        writer.Write(123456789);
        writer.Write(9876543210L);
        writer.Write(3.14159f);

        var reader = writer.ToReader();

        Assert.True(reader.ReadBoolean());
        Assert.False(reader.ReadBoolean());
        Assert.Equal((byte)123, reader.ReadByte());
        Assert.Equal((short)32000, reader.ReadInt16());
        Assert.Equal(123456789, reader.ReadInt32());
        Assert.Equal(9876543210L, reader.ReadInt64());
        Assert.Equal(3.14159f, reader.ReadSingle(), 5);
    }

    [Fact]
    public void WriteRead_StringAndBytes_ShouldMatch()
    {
        string testString = "Hello, Infinity!";
        byte[] testBytes = Encoding.UTF8.GetBytes("ByteArrayTest");

        var writer = new MessageWriter(_allocator);
        writer.Write(testString);
        writer.Write(testBytes);

        var reader = writer.ToReader();

        Assert.Equal(testString, reader.ReadString());

        var readBytes = reader.ReadBytes(testBytes.Length);
        Assert.Equal(testBytes, readBytes);
    }

    [Fact]
    public void WriteRead_PackedInt_ShouldMatch()
    {
        uint[] testValues = { 0, 1, 127, 128, 16384, 2097151, 268435455, uint.MaxValue };

        var writer = new MessageWriter(_allocator);
        foreach (var val in testValues)
            writer.WritePacked(val);

        var reader = writer.ToReader();

        foreach (var val in testValues)
            Assert.Equal(val, reader.ReadPackedUInt32());
    }

    [Fact]
    public void WriteRead_Span_ShouldMatch()
    {
        var data = new byte[256];
        for (int i = 0; i < data.Length; i++)
            data[i] = (byte)i;

        var writer = new MessageWriter(_allocator);
        writer.Write(data, 50, 100); // write a slice

        var reader = writer.ToReader();
        var slice = reader.ReadBytes(100);

        Assert.Equal(data.Skip(50).Take(100).ToArray(), slice);
    }

    [Fact]
    public void StressTest_LargeBuffer()
    {
        const int iterations = 10000;
        var writer = new MessageWriter(_allocator);
        var random = new Random(123);

        int[] ints = new int[iterations];
        for (int i = 0; i < iterations; i++)
        {
            ints[i] = random.Next();
            writer.Write(ints[i]);
        }

        var reader = writer.ToReader();

        for (int i = 0; i < iterations; i++)
        {
            Assert.Equal(ints[i], reader.ReadInt32());
        }
    }

    [Fact]
    public void StressTest_MixedWrites()
    {
        const int iterations = 5000;
        var writer = new MessageWriter(_allocator);
        var random = new Random(456);

        string[] strings = new string[iterations];
        for (int i = 0; i < iterations; i++)
        {
            strings[i] = Guid.NewGuid().ToString();
            writer.Write((byte)random.Next(0, 256));
            writer.Write((short)random.Next(short.MinValue, short.MaxValue));
            writer.WritePacked((uint)random.Next());
            writer.Write(strings[i]);
        }

        var reader = writer.ToReader();

        for (int i = 0; i < iterations; i++)
        {
            reader.ReadByte();
            reader.ReadInt16();
            reader.ReadPackedUInt32();
            Assert.Equal(strings[i], reader.ReadString());
        }
    }
}
