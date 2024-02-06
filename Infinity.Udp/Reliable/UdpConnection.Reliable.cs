using Infinity.Core;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        internal FasterConcurrentDictionary<ushort, Packet> reliable_data_packets_sent = new FasterConcurrentDictionary<ushort, Packet>();

        /// <summary>
        ///     Packet ids that have not been received, but are expected. 
        /// </summary>
        internal bool[] reliable_data_packets_missing = new bool[ushort.MaxValue + 1];

        private int last_id_allocated = -1;

        protected volatile ushort reliable_receive_last = ushort.MaxValue;

        internal void DisconnectInternalPacket(InfinityInternalErrors _error, string _reason)
        {
            DisconnectInternal(_error, _reason);
        }

        public int ManageReliablePackets()
        {
            int output = 0;
            if (reliable_data_packets_sent.Count > 0)
            {
                reliable_data_packets_sent.ForEach(id_packet =>
                {
                    Packet packet = id_packet.Value;

                    try
                    {
                        output += packet.Resend();
                    }
                    catch { }
                });
            }

            return output;
        }

        protected void ReliableSend(byte[] _buffer, Action _ack_callback = null)
        {
            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            AttachReliableID(_buffer, 1, _ack_callback);
            WriteBytesToConnection(_buffer, _buffer.Length);
            Statistics.LogReliableMessageSent(_buffer.Length);
        }

        private void ReliableMessageReceive(MessageReader _reader)
        {
            if (ProcessReliableReceive(_reader.Buffer, 1, out var id))
            {
                InvokeDataReceived(_reader);
            }
            else
            {
                _reader.Recycle();
            }
        }

        private bool ProcessReliableReceive(byte[] _bytes, int _offset, out ushort _id)
        {
            byte b1 = _bytes[_offset];
            byte b2 = _bytes[_offset + 1];

            //Get the ID form the packet
            _id = (ushort)((b1 << 8) + b2);

            /*
             * It gets a little complicated here (note the fact I'm actually using a multiline comment for once...)
             * 
             * In a simple world if our data is greater than the last reliable packet received (reliableReceiveLast)
             * then it is guaranteed to be a new packet, if it's not we can see if we are missing that packet (lookup 
             * in reliableDataPacketsMissing).
             * 
             * --------rrl#############             (1)
             * 
             * (where --- are packets received already and #### are packets that will be counted as new)
             * 
             * Unfortunately if id becomes greater than 65535 it will loop back to zero so we will add a pointer that
             * specifies any packets with an id behind it are also new (overwritePointer).
             * 
             * ####op----------rrl#####             (2)
             * 
             * ------rll#########op----             (3)
             * 
             * Anything behind than the reliableReceiveLast pointer (but greater than the overwritePointer is either a 
             * missing packet or something we've already received so when we change the pointers we need to make sure 
             * we keep note of what hasn't been received yet (reliableDataPacketsMissing).
             * 
             * So...
             */

            bool result = true;

            lock (reliable_data_packets_missing)
            {
                //Calculate overwritePointer
                ushort overwrite_pointer = (ushort)(reliable_receive_last - 32768);

                //Calculate if it is a new packet by examining if it is within the range
                bool is_new;
                if (overwrite_pointer < reliable_receive_last)
                {
                    is_new = _id > reliable_receive_last || _id <= overwrite_pointer;     //Figure (2)
                }
                else
                {
                    is_new = _id > reliable_receive_last && _id <= overwrite_pointer;     //Figure (3)
                }

                //If it's new or we've not received anything yet
                if (is_new)
                {
                    // Mark items between the most recent receive and the id received as missing
                    if (_id > reliable_receive_last)
                    {
                        for (ushort i = (ushort)(reliable_receive_last + 1); i < _id; i++)
                        {
                            reliable_data_packets_missing[i] = true;
                        }
                    }
                    else
                    {
                        int cnt = (ushort.MaxValue - reliable_receive_last) + _id;
                        for (ushort i = 1; i <= cnt; ++i)
                        {
                            reliable_data_packets_missing[(ushort)(i + reliable_receive_last)] = true;
                        }
                    }

                    //Update the most recently received
                    reliable_receive_last = _id;
                }

                //Else it could be a missing packet
                else
                {
                    //See if we're missing it, else this packet is a duplicate as so we return false
                    if (reliable_data_packets_missing[_id] == false)
                    {
                        result = false;
                    }
                    else
                    {
                        reliable_data_packets_missing[_id] = false;
                    }
                }
            }

            // Send an acknowledgement
            SendAck(_id);

            return result;
        }

        private void AcknowledgementMessageReceive(byte[] _bytes, int _bytes_received)
        {
            pings_since_ack = 0;

            ushort id = (ushort)((_bytes[1] << 8) + _bytes[2]);
            AcknowledgeMessageId(id);

            if (_bytes_received == 4)
            {
                byte recent_packets = _bytes[3];
                for (int i = 1; i <= 8; ++i)
                {
                    if ((recent_packets & 1) != 0)
                    {
                        AcknowledgeMessageId((ushort)(id - i));
                    }

                    recent_packets >>= 1;
                }
            }
        }

        private void AcknowledgeMessageId(ushort _id)
        {
            // Dispose of timer and remove from dictionary
            if (reliable_data_packets_sent.TryRemove(_id, out Packet packet))
            {
                Statistics.LogReliablePacketAcknowledged();
                float rt = packet.Stopwatch.ElapsedMilliseconds;

                packet.AckCallback?.Invoke();
                packet.Recycle();

                AveragePingMs = AveragePingMs * .7f + rt * .3f;
            }
            else if (active_pings.TryFindPing(_id, out DateTime pingPkt))
            {
                Statistics.LogReliablePacketAcknowledged();
                float rt = (float)(DateTime.UtcNow - pingPkt).TotalMilliseconds;

                AveragePingMs = AveragePingMs * .7f + rt * .3f;
            }
        }

        private void SendAck(ushort _id)
        {
            byte recent_packets = 0;
            lock (reliable_data_packets_missing)
            {
                for (int i = 1; i <= 8; ++i)
                {
                    var index = (ushort)(_id - i);
                    if (index >= 0)
                    {
                        if (!reliable_data_packets_missing[index])
                        {
                            recent_packets |= (byte)(1 << (i - 1));
                        }
                    }
                }
            }

            byte[] bytes = new byte[]
            {
                UdpSendOptionInternal.Acknowledgement,
                (byte)(_id >> 8),
                (byte)(_id >> 0),
                recent_packets
            };

            WriteBytesToConnection(bytes, bytes.Length);
        }

        private void DisposeReliablePackets()
        {
            reliable_data_packets_sent.ForEach(id_packet =>
            {
                ushort id = id_packet.Key;
                if (reliable_data_packets_sent.TryRemove(id, out var packet))
                {
                    packet.Recycle();
                }
            });
        }

        protected void AttachReliableID(byte[] _buffer, int _offset, Action _ack_callback = null)
        {
            ushort id = (ushort)++last_id_allocated;

            _buffer[_offset] = (byte)(id >> 8);
            _buffer[_offset + 1] = (byte)id;

            int resend_delay_ms = Configuration.Reliability.ResendTimeoutMs;
            if (resend_delay_ms <= 0)
            {
                resend_delay_ms = Math.Clamp((int)(AveragePingMs * Configuration.Reliability.ResendPingMultiplier), 
                    Packet.MinResendDelayMs, Packet.MaxInitialResendDelayMs);
            }

            Packet packet = Pools.PacketPool.GetObject();
            packet.Set(
                this,
                _buffer,
                resend_delay_ms,
                _ack_callback);

            if (!reliable_data_packets_sent.TryAdd(id, packet))
            {
                throw new Exception("That shouldn't be possible");
            }
        }
    }
}
