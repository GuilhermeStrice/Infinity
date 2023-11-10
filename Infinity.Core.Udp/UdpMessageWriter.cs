namespace Infinity.Core.Udp
{
    public class UdpMessageWriter : MessageWriter
    {
        public static readonly ObjectPool<UdpMessageWriter> WriterPool = new ObjectPool<UdpMessageWriter>(() => new UdpMessageWriter(BufferSize));

        private Stack<int> messageStarts = new Stack<int>();

        public UdpSendOption SendOption { get; private set; }

        public override void Recycle()
        {
            Position = Length = 0;
            WriterPool.PutObject(this);
        }

        public UdpMessageWriter(byte[] buffer) : base(buffer)
        {
        }

        public UdpMessageWriter(int bufferSize) : base(bufferSize)
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
                switch (SendOption)
                {
                    case UdpSendOption.Reliable:
                        {
                            byte[] output = new byte[Length - 3];
                            System.Buffer.BlockCopy(Buffer, 3, output, 0, Length - 3);
                            return output;
                        }
                    case UdpSendOption.None:
                        {
                            byte[] output = new byte[Length - 1];
                            System.Buffer.BlockCopy(Buffer, 1, output, 0, Length - 1);
                            return output;
                        }
                }
            }

            throw new NotImplementedException();
        }

        public static UdpMessageWriter Get(UdpSendOption sendOption = UdpSendOption.None)
        {
            var output = WriterPool.GetObject();
            output.Clear(sendOption);

            return output;
        }

        public void Clear(UdpSendOption sendOption)
        {
            Array.Clear(Buffer, 0, Buffer.Length);
            messageStarts.Clear();
            SendOption = sendOption;
            Buffer[0] = (byte)sendOption;
            switch (sendOption)
            {
                default:
                case UdpSendOption.None:
                    Length = Position = 1;
                    break;
                case UdpSendOption.Reliable:
                    Length = Position = 3;
                    break;
            }
        }

        public void Write(UdpMessageWriter msg, bool includeHeader)
        {
            int offset = 0;
            if (!includeHeader)
            {
                switch (msg.SendOption)
                {
                    case UdpSendOption.None:
                        offset = 1;
                        break;
                    case UdpSendOption.Reliable:
                        offset = 3;
                        break;
                }
            }

            Write(msg.Buffer, offset, msg.Length - offset);
        }

        public override bool HasBytes(int expected)
        {
            if (SendOption == UdpSendOption.None)
            {
                return Length > 1 + expected;
            }

            return Length > 3 + expected;
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
