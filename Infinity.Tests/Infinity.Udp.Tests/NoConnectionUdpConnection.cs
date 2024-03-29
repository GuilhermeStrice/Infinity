﻿using Infinity.Core;

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

        public void Test_Receive(MessageWriter msg)
        {
            byte[] buffer = new byte[msg.Length];
            Array.Copy(msg.Buffer, 0, buffer, 0, msg.Length);

            var data = MessageReader.Get(buffer);
            HandleReceive(data, data.Length);
        }

        public override void WriteBytesToConnection(byte[] _bytes, int _length)
        {
            BytesSent.Add(MessageReader.Get(_bytes));
        }

        public override void Connect(MessageWriter _writer, int _timeout = 5000)
        {
            State = ConnectionState.Connected;
        }

        public override void ConnectAsync(MessageWriter _writer)
        {
            State = ConnectionState.Connected;
        }

        protected override void DisconnectRemote(string _reason, MessageReader _reader)
        {
            
        }

        protected override void SetState(ConnectionState _state)
        {
        }

        protected override void DisconnectInternal(InfinityInternalErrors _error, string _reason)
        {
        }

        public override void WriteBytesToConnectionSync(byte[] _bytes, int _length)
        {
            throw new NotImplementedException();
        }

        protected override void ShareConfiguration()
        {
            throw new NotImplementedException();
        }

        protected override void ReadConfiguration(MessageReader _reader)
        {
            throw new NotImplementedException();
        }
    }
}
