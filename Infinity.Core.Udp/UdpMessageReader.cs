namespace Infinity.Core.Udp
{
    public class UdpMessageReader : MessageReader
    {
        public static readonly ObjectPool<UdpMessageReader> ReaderPool = new ObjectPool<UdpMessageReader>(() => new UdpMessageReader());

        public UdpMessageReader()
        {
        }

        public static UdpMessageReader Get(byte[] buffer)
        {
            var output = ReaderPool.GetObject();

            output.Buffer = buffer;
            output.Offset = 0;
            output.Position = 0;
            output.Length = buffer.Length;
            output.Tag = byte.MaxValue;

            return output;
        }

        public static UdpMessageReader GetSized(int minSize)
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
            output.Tag = byte.MaxValue;
            return output;
        }

        public static UdpMessageReader CopyMessageIntoParent(MessageReader source)
        {
            var output = GetSized(source.Length + 3);
            System.Buffer.BlockCopy(source.Buffer, source.Offset - 3, output.Buffer, 0, source.Length + 3);

            output.Offset = 0;
            output.Position = 0;
            output.Length = source.Length + 3;

            return output;
        }

        public static UdpMessageReader Get(UdpMessageReader source)
        {
            var output = GetSized(source.Buffer.Length);
            System.Buffer.BlockCopy(source.Buffer, 0, output.Buffer, 0, source.Buffer.Length);

            output.Offset = source.Offset;

            output._position = source._position;
            output.readHead = source.readHead;

            output.Length = source.Length;
            output.Tag = source.Tag;

            return output;
        }

        public static UdpMessageReader? Get(byte[] buffer, int offset)
        {
            // Ensure there is at least a header
            if (offset + 3 > buffer.Length)
                return null;

            var output = ReaderPool.GetObject();

            output.Buffer = buffer;
            output.Offset = offset;
            output.Position = 0;

            output.Length = output.ReadUInt16();
            output.Tag = output.ReadByte();

            output.Offset += 3;
            output.Position = 0;

            return output;
        }

        public override void InsertMessage(MessageReader _reader, MessageWriter _writer)
        {
            var reader = (UdpMessageReader)_reader;
            var writer = (UdpMessageWriter)_writer;

            var temp = GetSized(reader.Buffer.Length);
            try
            {
                var headerOffset = reader.Offset - 3;
                var startOfMessage = reader.Offset;
                var len = reader.Buffer.Length - startOfMessage;
                int writerOffset = 3;
                switch (writer.SendOption)
                {
                    case UdpSendOption.Reliable:
                        writerOffset = 3;
                        break;
                    case UdpSendOption.None:
                        writerOffset = 1;
                        break;
                }

                //store the original buffer in temp
                Array.Copy(reader.Buffer, headerOffset, temp.Buffer, 0, len);

                //put the contents of writer in at headerOffset
                Array.Copy(writer.Buffer, writerOffset, Buffer, headerOffset, writer.Length - writerOffset);

                //put the original buffer in after that
                Array.Copy(temp.Buffer, 0, Buffer, headerOffset + (writer.Length - writerOffset), len - writer.Length);

                AdjustLength(-1 * reader.Offset, -1 * (writer.Length - writerOffset));
            }
            finally
            {
                temp.Recycle();
            }
        }

        public override MessageReader ReadMessage()
        {
            // Ensure there is at least a header
            if (BytesRemaining < 3)
                throw new InvalidDataException($"ReadMessage header is longer than message length: 3 of {BytesRemaining}");

            var output = new UdpMessageReader();

            output.Parent = this;
            output.Buffer = Buffer;
            output.Offset = readHead;
            output.Position = 0;

            output.Length = output.ReadUInt16();
            output.Tag = output.ReadByte();

            output.Offset += 3;
            output.Position = 0;

            if (BytesRemaining < output.Length + 3)
                throw new InvalidDataException($"Message Length at Position {readHead} is longer than message length: {output.Length + 3} of {BytesRemaining}");

            Position += output.Length + 3;
            return output;
        }

        public override void Recycle()
        {
            Parent = null;
            ReaderPool.PutObject(this);
        }

        protected override void AdjustLength(int offset, int amount)
        {
            if (readHead > offset)
            {
                Position -= amount;
            }

            if (Parent != null)
            {
                var lengthOffset = Offset - 3;
                var curLen = Buffer[lengthOffset] | (Buffer[lengthOffset + 1] << 8);

                curLen -= amount;
                Length -= amount;

                Buffer[lengthOffset] = (byte)curLen;
                Buffer[lengthOffset + 1] = (byte)(Buffer[lengthOffset + 1] >> 8);

                ((UdpMessageReader)Parent).AdjustLength(offset, amount);
            }
        }

        public override void RemoveMessage(MessageReader reader)
        {
            var temp = GetSized(reader.Buffer.Length);
            try
            {
                var headerOffset = reader.Offset - 3;
                var endOfMessage = reader.Offset + reader.Length;
                var len = reader.Buffer.Length - endOfMessage;

                Array.Copy(reader.Buffer, endOfMessage, temp.Buffer, 0, len);
                Array.Copy(temp.Buffer, 0, Buffer, headerOffset, len);

                AdjustLength(reader.Offset, reader.Length + 3);
            }
            finally
            {
                temp.Recycle();
            }
        }

        public override MessageReader Duplicate()
        {
            var output = GetSized(Length);
            Array.Copy(Buffer, Offset, output.Buffer, 0, Length);
            output.Length = Length;
            output.Offset = 0;
            output.Position = 0;

            return output;
        }

        public override MessageReader ReadMessageAsNewBuffer()
        {
            if (BytesRemaining < 3)
                throw new InvalidDataException($"ReadMessage header is longer than message length: 3 of {BytesRemaining}");

            var len = ReadUInt16();
            var tag = ReadByte();

            if (BytesRemaining < len)
                throw new InvalidDataException($"Message Length at Position {readHead} is longer than message length: {len} of {BytesRemaining}");

            var output = GetSized(len);

            Array.Copy(Buffer, readHead, output.Buffer, 0, len);

            output.Length = len;
            output.Tag = tag;

            Position += output.Length;
            return output;
        }
    }
}
