namespace Infinity.Core.Tcp
{
    public class TcpMessageWriter : MessageWriter
    {
        public static readonly ObjectPool<TcpMessageWriter> WriterPool = new ObjectPool<TcpMessageWriter>(() => new TcpMessageWriter(BufferSize));

        private Stack<int> messageStarts = new Stack<int>();

        public TcpSendOption SendOption { get; private set; }

        public override void Recycle()
        {
            Position = Length = 0;
            WriterPool.PutObject(this);
        }

        public TcpMessageWriter(byte[] buffer) : base(buffer)
        {
        }

        public TcpMessageWriter(int bufferSize) : base(bufferSize)
        {
        }

        public byte[] ToByteArray(bool includeHeader)
        {
            if (includeHeader)
            {
                byte[] output = new byte[Length];
                System.Buffer.BlockCopy(Buffer, 0, output, 0, Length);
                return output;
            }
            else
            {
                byte[] output = new byte[Length - 1];
                System.Buffer.BlockCopy(Buffer, 1, output, 0, Length - 1);
                return output;
            }

            throw new NotImplementedException();
        }

        public static TcpMessageWriter Get(TcpSendOption sendOption = TcpSendOption.MessageUnordered)
        {
            var output = WriterPool.GetObject();
            output.Clear(sendOption);

            return output;
        }

        public void Clear(TcpSendOption sendOption)
        {
            Array.Clear(Buffer, 0, Buffer.Length);
            messageStarts.Clear();
            SendOption = sendOption;
            Buffer[0] = (byte)sendOption;
            Length = Position = 1;
        }

        public void Write(TcpMessageWriter msg, bool includeHeader)
        {
            int offset = 0;
            if (!includeHeader)
                offset = 1;

            Write(msg.Buffer, offset, msg.Length - offset);
        }

        public override bool HasBytes(int expected)
        {
            return Length > 1 + expected;
        }

        public void StartMessage(byte typeFlag)
        {
            var messageStart = Position;
            messageStarts.Push(messageStart);
            Buffer[messageStart] = 0;
            Buffer[messageStart + 1] = 0;
            Position += 2;
            Write(typeFlag);
        }

        public void EndMessage()
        {
            var lastMessageStart = messageStarts.Pop();
            ushort length = (ushort)(Position - lastMessageStart - 3); // Minus length and type byte
            Buffer[lastMessageStart] = (byte)length;
            Buffer[lastMessageStart + 1] = (byte)(length >> 8);
        }

        public void CancelMessage()
        {
            Position = messageStarts.Pop();
            Length = Position;
        }
    }
}
