using Infinity.Core.Udp;

namespace Infinity.Core.Tests
{
    public class UdpMessageWriterTests
    {

        [Fact]
        public void CancelMessages()
        {
            var msg = new UdpMessageWriter(128);

            msg.StartMessage(1);
            msg.Write(32);

            msg.StartMessage(2);
            msg.Write(2);
            msg.CancelMessage();

            Assert.Equal(7, msg.Length);
            Assert.False(msg.HasBytes(7));

            msg.CancelMessage();

            Assert.Equal(0, msg.Length);
            Assert.False(msg.HasBytes(1));
        }

        [Fact]
        public void HasBytes()
        {
            var msg = new UdpMessageWriter(128);

            msg.StartMessage(1);
            msg.Write(32);

            msg.StartMessage(2);
            msg.Write(2);
            msg.EndMessage();

            // Assert.Equal(7, msg.Length);
            Assert.True(msg.HasBytes(7));
        }

        [Fact]
        public void WriteProperInt()
        {
            const int Test1 = int.MaxValue;
            const int Test2 = int.MinValue;

            var msg = new UdpMessageWriter(128);
            msg.Write(Test1);
            msg.Write(Test2);

            Assert.Equal(8, msg.Length);
            Assert.Equal(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.Equal(Test1, reader.ReadInt32());
                Assert.Equal(Test2, reader.ReadInt32());
            }
        }

        [Fact]
        public void WriteProperBool()
        {
            const bool Test1 = true;
            const bool Test2 = false;

            var msg = new UdpMessageWriter(128);
            msg.Write(Test1);
            msg.Write(Test2);

            Assert.Equal(2, msg.Length);
            Assert.Equal(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.Equal(Test1, reader.ReadBoolean());
                Assert.Equal(Test2, reader.ReadBoolean());
            }
        }

        [Fact]
        public void WriteProperString()
        {
            const string Test1 = "Hello";
            string Test2 = new string(' ', 1024);
            var msg = new UdpMessageWriter(2048);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.Write(string.Empty);

            Assert.Equal(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.Equal(Test1, reader.ReadString());
                Assert.Equal(Test2, reader.ReadString());
                Assert.Equal(string.Empty, reader.ReadString());
            }
        }

        [Fact]
        public void WriteProperFloat()
        {
            const float Test1 = 12.34f;

            var msg = new UdpMessageWriter(2048);
            msg.Write(Test1);

            Assert.Equal(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.Equal(Test1, reader.ReadSingle());
            }
        }

        [Fact]
        public void WritePackedUint()
        {
            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.WritePacked(8u);
            msg.WritePacked(250u);
            msg.WritePacked(68000u);
            msg.EndMessage();

            Assert.Equal(3 + 1 + 2 + 3, msg.Position);
            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);

            Assert.Equal(8u, reader.ReadPackedUInt32());
            Assert.Equal(250u, reader.ReadPackedUInt32());
            Assert.Equal(68000u, reader.ReadPackedUInt32());
        }

        [Fact]
        public void WritePackedInt()
        {
            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.WritePacked(8);
            msg.WritePacked(250);
            msg.WritePacked(68000);
            msg.WritePacked(60168000);
            msg.WritePacked(-68000);
            msg.WritePacked(-250);
            msg.WritePacked(-8);

            msg.WritePacked(0);
            msg.WritePacked(-1);
            msg.WritePacked(int.MinValue);
            msg.WritePacked(int.MaxValue);
            msg.EndMessage();

            Assert.Equal(3 + 1 + 2 + 3 + 4 + 5 + 5 + 5 + 1 + 5 + 5 + 5, msg.Position);
            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);

            Assert.Equal(8, reader.ReadPackedInt32());
            Assert.Equal(250, reader.ReadPackedInt32());
            Assert.Equal(68000, reader.ReadPackedInt32());
            Assert.Equal(60168000, reader.ReadPackedInt32());

            Assert.Equal(-68000, reader.ReadPackedInt32());
            Assert.Equal(-250, reader.ReadPackedInt32());
            Assert.Equal(-8, reader.ReadPackedInt32());

            Assert.Equal(0, reader.ReadPackedInt32());
            Assert.Equal(-1, reader.ReadPackedInt32());
            Assert.Equal(int.MinValue, reader.ReadPackedInt32());
            Assert.Equal(int.MaxValue, reader.ReadPackedInt32());
        }

        [Fact]
        public void WritesMessageLength()
        {
            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.EndMessage();

            Assert.Equal(2 + 1 + 4, msg.Position);
            Assert.Equal(msg.Length, msg.Position);

            using (MemoryStream m = new MemoryStream(msg.Buffer, 0, msg.Length))
            using (BinaryReader reader = new BinaryReader(m))
            {
                Assert.Equal(4, reader.ReadUInt16()); // Length After Type and Target
                Assert.Equal(1, reader.ReadByte()); // Type
                Assert.Equal(65534, reader.ReadInt32()); // Content
            }
        }

        [Fact]
        public void GetLittleEndian()
        {
            Assert.True(MessageWriter.IsLittleEndian());
        }
    }
}