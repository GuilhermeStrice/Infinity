using System.Threading.Tasks;
using Infinity.Core;

namespace Infinity.Udp.Tests
{
    public class NoConnectionUdpConnection : UdpConnection
    {
        public List<MessageReader> BytesSent = new List<MessageReader>();

        public NoConnectionUdpConnection(ILogger _logger) : base(_logger)
        {
        }

        public ushort ReliableReceiveLast => reliable_receive_last;

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

        public async Task Test_Receive(MessageWriter msg)
        {
            byte[] buffer = new byte[msg.Length];
            Array.Copy(msg.Buffer.ToArray(), 0, buffer, 0, msg.Length);

            var data = new MessageReader(new ChunkAllocator(1024), buffer, 0, buffer.Length);
            await HandleReceive(data, data.Length);
        }

        public override async Task WriteBytesToConnection(MessageWriter _writer)
        {
            BytesSent.Add(new MessageReader(new ChunkAllocator(1024), _writer.Buffer.ToArray(), 0, _writer.Length));
        }

        public override async Task Connect(MessageWriter _writer, int _timeout = 5000)
        {
            State = ConnectionState.Connected;
        }

        protected override async Task DisconnectRemote(string _reason, MessageReader _reader)
        {
            
        }

        protected override void SetState(ConnectionState _state)
        {
        }

        protected override async Task DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
        }

        public override void WriteBytesToConnectionSync(MessageWriter _writer)
        {
            throw new NotImplementedException();
        }

        protected override async Task ShareConfiguration()
        {
            throw new NotImplementedException();
        }

        protected override async Task ReadConfiguration(MessageReader _reader)
        {
            throw new NotImplementedException();
        }
    }
}
