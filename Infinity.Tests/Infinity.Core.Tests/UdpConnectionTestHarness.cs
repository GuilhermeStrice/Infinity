using Infinity.Core.Udp;
using System.Net.Sockets;

namespace Infinity.Core.Tests
{
    internal class UdpConnectionTestHarness : UdpConnection
    {
        public List<MessageReader> BytesSent = new List<MessageReader>();

        public UdpConnectionTestHarness() : base(new TestLogger())
        {
        }

        public ushort ReliableReceiveLast => reliableReceiveLast;


        public override void Connect(byte[] bytes = null, int timeout = 5000)
        {
            State = ConnectionState.Connected;
        }

        public override void ConnectAsync(byte[] bytes = null)
        {
            State = ConnectionState.Connected;
        }

        protected override bool SendDisconnect(MessageWriter writer)
        {
            lock (this)
            {
                if (State != ConnectionState.Connected)
                {
                    return false;
                }

                State = ConnectionState.NotConnected;
            }

            return true;
        }

        public override void WriteBytesToConnection(byte[] bytes, int length)
        {
            BytesSent.Add(MessageReader.Get(bytes));
        }

        public void Test_Receive(MessageWriter msg)
        {
            byte[] buffer = new byte[msg.Length];
            Buffer.BlockCopy(msg.Buffer, 0, buffer, 0, msg.Length);

            var data = MessageReader.Get(buffer);
            HandleReceive(data, data.Length);
        }
    }
}
