using Infinity.Core.Udp;

namespace Infinity.Core.Tests
{
    public class UdpMessageReaderTests
    {
        [Fact]
        public void ReadProperInt()
        {
            const int Test1 = int.MaxValue;
            const int Test2 = int.MinValue;

            var msg = new UdpMessageWriter(128);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.EndMessage();

            Assert.Equal(11, msg.Length);
            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);
            Assert.Equal(Test1, reader.ReadInt32());
            Assert.Equal(Test2, reader.ReadInt32());
        }

        [Fact]
        public void ReadProperBool()
        {
            const bool Test1 = true;
            const bool Test2 = false;

            var msg = new UdpMessageWriter(128);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.EndMessage();

            Assert.Equal(5, msg.Length);
            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);

            Assert.Equal(Test1, reader.ReadBoolean());
            Assert.Equal(Test2, reader.ReadBoolean());

        }

        [Fact]
        public void ReadProperString()
        {
            const string Test1 = "Hello";
            string Test2 = new string(' ', 1024);
            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.Write(string.Empty);
            msg.EndMessage();

            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);

            Assert.Equal(Test1, reader.ReadString());
            Assert.Equal(Test2, reader.ReadString());
            Assert.Equal(string.Empty, reader.ReadString());

        }

        [Fact]
        public void ReadProperFloat()
        {
            const float Test1 = 12.34f;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(Test1);
            msg.EndMessage();

            Assert.Equal(7, msg.Length);
            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);

            Assert.Equal(Test1, reader.ReadSingle());
        }

        [Fact]
        public void RemoveMessageWorks()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            var reader = UdpMessageReader.Get(msg.Buffer);
            reader.Length = msg.Length;

            var zero = reader.ReadMessage();

            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();
            two.RemoveMessage(three);

            // Reader becomes invalid
            Assert.NotEqual(Test3, three.ReadByte());

            // Unrealistic, but nice. Earlier data is not affected
            Assert.Equal(Test0, zero.ReadByte());

            // Continuing to read depth-first works
            var four = two.ReadMessage();
            Assert.Equal(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.Equal(Test5, five.ReadByte());
        }

        [Fact]
        public void InsertMessageWorks()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            var reader = UdpMessageReader.Get(msg.Buffer);

            var writer = UdpMessageWriter.Get(UdpSendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.Equal(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            var insert = two.ReadMessage();
            Assert.Equal(TestInsert, insert.ReadByte());
            three = two.ReadMessage();
            Assert.Equal(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.Equal(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.Equal(Test5, five.ReadByte());
        }

        [Fact]
        public void InsertMessageWorksWithSendOptionNone()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            var reader = UdpMessageReader.Get(msg.Buffer);

            var writer = UdpMessageWriter.Get(UdpSendOption.None);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.Equal(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            var insert = two.ReadMessage();
            Assert.Equal(TestInsert, insert.ReadByte());
            three = two.ReadMessage();
            Assert.Equal(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.Equal(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.Equal(Test5, five.ReadByte());

        }

        [Fact]
        public void InsertMessageWithoutStartMessageInWriter()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            var reader = UdpMessageReader.Get(msg.Buffer);

            MessageWriter writer = UdpMessageWriter.Get(UdpSendOption.Reliable);
            writer.Write(TestInsert);

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.Equal(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            Assert.Equal(TestInsert, two.ReadByte());
            three = two.ReadMessage();
            Assert.Equal(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.Equal(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.Equal(Test5, five.ReadByte());
        }

        [Fact]
        public void InsertMessageWithMultipleMessagesInWriter()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte TestInsert = 66;
            const byte TestInsert2 = 77;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();
            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            var reader = UdpMessageReader.Get(msg.Buffer);

            var writer = UdpMessageWriter.Get(UdpSendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            writer.StartMessage(6);
            writer.Write(TestInsert2);
            writer.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            //set the position back to zero to read back the updated message
            reader.Position = 0;

            var zero = reader.ReadMessage();
            Assert.Equal(Test0, zero.ReadByte());
            one = reader.ReadMessage();
            two = one.ReadMessage();
            var insert = two.ReadMessage();
            Assert.Equal(TestInsert, insert.ReadByte());
            var insert2 = two.ReadMessage();
            Assert.Equal(TestInsert2, insert2.ReadByte());
            three = two.ReadMessage();
            Assert.Equal(Test3, three.ReadByte());
            var four = two.ReadMessage();
            Assert.Equal(Test4, four.ReadByte());

            var five = reader.ReadMessage();
            Assert.Equal(Test5, five.ReadByte());
        }

        [Fact]
        public void InsertMessageMultipleInsertsWithoutReset()
        {
            const byte Test0 = 11;
            const byte Test3 = 33;
            const byte Test4 = 44;
            const byte Test5 = 55;
            const byte Test6 = 66;
            const byte TestInsert = 77;
            const byte TestInsert2 = 88;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(0);
            msg.Write(Test0);
            msg.EndMessage();

            msg.StartMessage(12);
            msg.StartMessage(23);

            msg.StartMessage(34);
            msg.Write(Test3);
            msg.EndMessage();

            msg.StartMessage(45);
            msg.Write(Test4);
            msg.EndMessage();

            msg.EndMessage();

            msg.StartMessage(56);
            msg.Write(Test5);
            msg.EndMessage();

            msg.EndMessage();

            msg.StartMessage(67);
            msg.Write(Test6);
            msg.EndMessage();

            var reader = UdpMessageReader.Get(msg.Buffer);

            var writer = UdpMessageWriter.Get(UdpSendOption.Reliable);
            writer.StartMessage(5);
            writer.Write(TestInsert);
            writer.EndMessage();

            var writer2 = UdpMessageWriter.Get(UdpSendOption.Reliable);
            writer2.StartMessage(6);
            writer2.Write(TestInsert2);
            writer2.EndMessage();

            reader.ReadMessage();
            var one = reader.ReadMessage();
            var two = one.ReadMessage();
            var three = two.ReadMessage();

            two.InsertMessage(three, writer);

            // three becomes invalid
            Assert.NotEqual(Test3, three.ReadByte());

            // Continuing to read works
            var four = two.ReadMessage();
            Assert.Equal(Test4, four.ReadByte());

            var five = one.ReadMessage();
            Assert.Equal(Test5, five.ReadByte());

            reader.InsertMessage(one, writer2);

            var six = reader.ReadMessage();
            Assert.Equal(Test6, six.ReadByte());
        }

        [Fact]
        public void CopySubMessage()
        {
            const byte Test1 = 12;
            const byte Test2 = 146;

            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(1);

            msg.StartMessage(2);
            msg.Write(Test1);
            msg.Write(Test2);
            msg.EndMessage();

            msg.EndMessage();

            var handleMessage = UdpMessageReader.Get(msg.Buffer, 0);
            Assert.Equal(1, handleMessage.Tag);

            var parentReader = UdpMessageReader.Get(handleMessage);

            handleMessage.Recycle();
            SetZero(handleMessage);

            Assert.Equal(1, parentReader.Tag);

            for (int i = 0; i < 5; ++i)
            {

                var reader = parentReader.ReadMessage();
                Assert.Equal(2, reader.Tag);
                Assert.Equal(Test1, reader.ReadByte());
                Assert.Equal(Test2, reader.ReadByte());

                var temp = parentReader;
                parentReader = UdpMessageReader.CopyMessageIntoParent(reader);

                temp.Recycle();
                SetZero(temp);
                SetZero(reader);
            }
        }

        [Fact]
        public void ReadMessageLength()
        {
            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.StartMessage(2);
            msg.Write("HO");
            msg.EndMessage();
            msg.StartMessage(2);
            msg.EndMessage();
            msg.EndMessage();

            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);
            Assert.Equal(1, reader.Tag);
            Assert.Equal(65534, reader.ReadInt32()); // Content

            var sub = reader.ReadMessage();
            Assert.Equal(3, sub.Length);
            Assert.Equal(2, sub.Tag);
            Assert.Equal("HO", sub.ReadString());

            sub = reader.ReadMessage();
            Assert.Equal(0, sub.Length);
            Assert.Equal(2, sub.Tag);
        }

        [Fact]
        public void ReadMessageAsNewBufferLength()
        {
            var msg = new UdpMessageWriter(2048);
            msg.StartMessage(1);
            msg.Write(65534);
            msg.StartMessage(2);
            msg.Write("HO");
            msg.EndMessage();
            msg.StartMessage(232);
            msg.EndMessage();
            msg.EndMessage();

            Assert.Equal(msg.Length, msg.Position);

            var reader = UdpMessageReader.Get(msg.Buffer, 0);
            Assert.Equal(1, reader.Tag);
            Assert.Equal(65534, reader.ReadInt32()); // Content

            var sub = reader.ReadMessageAsNewBuffer();
            Assert.Equal(0, sub.Position);
            Assert.Equal(0, sub.Offset);

            Assert.Equal(3, sub.Length);
            Assert.Equal(2, sub.Tag);
            Assert.Equal("HO", sub.ReadString());

            sub.Recycle();

            sub = reader.ReadMessageAsNewBuffer();
            Assert.Equal(0, sub.Position);
            Assert.Equal(0, sub.Offset);

            Assert.Equal(0, sub.Length);
            Assert.Equal(232, sub.Tag);
            sub.Recycle();
        }

        [Fact]
        public void ReadStringProtectsAgainstOverrun()
        {
            const string TestDataFromAPreviousPacket = "You shouldn't be able to see this data";

            // An extra byte from the length of TestData when written via MessageWriter
            int DataLength = TestDataFromAPreviousPacket.Length + 1;

            // THE BUG
            //
            // No bound checks. When the server wants to read a string from
            // an offset, it reads the packed int at that offset, treats it
            // as a length and then proceeds to read the data that comes after
            // it without any bound checks. This can be chained with something
            // else to create an infoleak.

            var writer = UdpMessageWriter.Get(UdpSendOption.None);

            // This will be our malicious "string length"
            writer.WritePacked(DataLength);

            // This is data from a "previous packet"
            writer.Write(TestDataFromAPreviousPacket);

            byte[] testData = writer.ToByteArray(includeHeader: false);

            // One extra byte for the MessageWriter header, one more for the malicious data
            Assert.Equal(DataLength + 1, testData.Length);

            var dut = UdpMessageReader.Get(testData);

            // If Length is short by even a byte, ReadString should obey that.
            dut.Length--;

            try
            {
                dut.ReadString();
                Assert.Fail("ReadString is expected to throw");
            }
            catch (InvalidDataException) { }
        }

        [Fact]
        public void ReadMessageProtectsAgainstOverrun()
        {
            const string TestDataFromAPreviousPacket = "You shouldn't be able to see this data";

            // An extra byte from the length of TestData when written via MessageWriter
            // Extra 3 bytes for the length + tag header for ReadMessage.
            int DataLength = TestDataFromAPreviousPacket.Length + 1 + 3;

            // THE BUG
            //
            // No bound checks. When the server wants to read a message, it
            // reads the uint16 at that offset, treats it as a length without any bound checks.
            // This can be allow a later ReadString or ReadBytes to create an infoleak.

            var writer = UdpMessageWriter.Get(UdpSendOption.None);

            // This is the malicious length. No data in this message, so it should be zero.
            writer.Write((ushort)1);
            writer.Write((byte)0); // Tag

            // This is data from a "previous packet"
            writer.Write(TestDataFromAPreviousPacket);

            byte[] testData = writer.ToByteArray(false);

            Assert.Equal(DataLength, testData.Length);

            var outer = UdpMessageReader.Get(testData);

            // Length is just the malicious message header.
            outer.Length = 3;

            try
            {
                outer.ReadMessage();
                Assert.Fail("ReadMessage is expected to throw");
            }
            catch (InvalidDataException) { }
        }

        [Fact]
        public void GetLittleEndian()
        {
            Assert.True(MessageWriter.IsLittleEndian());
        }

        private void SetZero(MessageReader reader)
        {
            for (int i = 0; i < reader.Buffer.Length; ++i)
                reader.Buffer[i] = 0;
        }
    }
}
