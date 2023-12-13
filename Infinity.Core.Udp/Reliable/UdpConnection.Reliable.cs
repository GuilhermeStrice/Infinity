using System.Collections.Concurrent;

namespace Infinity.Core.Udp
{
    partial class UdpConnection
    {
        internal readonly ObjectPool<Packet> PacketPool;

        /// <summary>
        ///     The starting timeout, in miliseconds, at which data will be resent.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         For reliable delivery data is resent at specified intervals unless an acknowledgement is received from the 
        ///         receiving device. The ResendTimeout specifies the interval between the packets being resent, each time a packet
        ///         is resent the interval is increased for that packet until the duration exceeds the <see cref="DisconnectTimeoutMs"/> value.
        ///     </para>
        ///     <para>
        ///         Setting this to its default of 0 will mean the timeout is 2 times the value of the average ping, usually 
        ///         resulting in a more dynamic resend that responds to endpoints on slower or faster connections.
        ///     </para>
        /// </remarks>
        public volatile int ResendTimeoutMs = 0;

        /// <summary>
        /// Max number of times to resend. 0 == no limit
        /// </summary>
        public volatile int ResendLimit = 0;

        /// <summary>
        /// A compounding multiplier to back off resend timeout.
        /// Applied to ping before first timeout when ResendTimeout == 0.
        /// </summary>
        public volatile float ResendPingMultiplier = 2;

        /// <summary>
        ///     Holds the last ID allocated.
        /// </summary>
        private int last_id_allocated = -1;

        /// <summary>
        ///     The packets of data that have been transmitted reliably and not acknowledged.
        /// </summary>
        internal ConcurrentDictionary<ushort, Packet> reliable_data_packets_sent = new ConcurrentDictionary<ushort, Packet>();

        /// <summary>
        ///     Packet ids that have not been received, but are expected. 
        /// </summary>
        private HashSet<ushort> reliable_data_packets_missing = new HashSet<ushort>();

        /// <summary>
        ///     The packet id that was received last.
        /// </summary>
        protected volatile ushort reliable_receive_last = ushort.MaxValue;

        private object ping_lock = new object();

        /// <summary>
        ///     Returns the average ping to this endpoint.
        /// </summary>
        /// <remarks>
        ///     This returns the average ping for a one-way trip as calculated from the reliable packets that have been sent 
        ///     and acknowledged by the endpoint.
        /// </remarks>
        private float ping_ms = 500;

        /// <summary>
        ///     The maximum times a message should be resent before marking the endpoint as disconnected.
        /// </summary>
        /// <remarks>
        ///     Reliable packets will be resent at an interval defined in <see cref="ResendTimeoutMs"/> for the number of times
        ///     specified here. Once a packet has been retransmitted this number of times and has not been acknowledged the
        ///     connection will be marked as disconnected and the <see cref="Connection.Disconnected">Disconnected</see> event
        ///     will be invoked.
        /// </remarks>
        public volatile int DisconnectTimeoutMs = 5000;

        public int ManageReliablePackets()
        {
            int output = 0;
            if (reliable_data_packets_sent.Count > 0)
            {
                foreach (var id_packet in reliable_data_packets_sent)
                {
                    Packet packet = id_packet.Value;

                    try
                    {
                        output += packet.Resend();
                    }
                    catch { }
                }
            }

            return output;
        }

        /// <summary>
        ///     Adds a 2 byte ID to the packet at offset and stores the packet reference for retransmission.
        /// </summary>
        /// <param name="_buffer">The buffer to attach to.</param>
        /// <param name="_offset">The offset to attach at.</param>
        /// <param name="_ack_callback">The callback to make once the packet has been acknowledged.</param>
        protected void AttachReliableID(byte[] _buffer, int _offset, Action _ack_callback = null)
        {
            ushort id = (ushort)Interlocked.Increment(ref last_id_allocated);

            _buffer[_offset] = (byte)(id >> 8);
            _buffer[_offset + 1] = (byte)id;

            int resend_delay_ms = ResendTimeoutMs;
            if (resend_delay_ms <= 0)
            {
                resend_delay_ms = Math.Clamp((int)(ping_ms * ResendPingMultiplier), Packet.MinResendDelayMs, Packet.MaxInitialResendDelayMs);
            }

            Packet packet = PacketPool.GetObject();
            packet.Set(
                id,
                _buffer,
                _buffer.Length,
                resend_delay_ms,
                _ack_callback);

            if (!reliable_data_packets_sent.TryAdd(id, packet))
            {
                throw new Exception("That shouldn't be possible");
            }
        }

        /// <summary>
        ///     Sends the bytes reliably and stores the send.
        /// </summary>
        /// <param name="_send_option"></param>
        /// <param name="_data">The byte array to write to.</param>
        /// <param name="_ack_callback">The callback to make once the packet has been acknowledged.</param>
        private void ReliableSend(byte _send_option, byte[] _data, Action _ack_callback = null)
        {
            //Inform keepalive not to send for a while
            ResetKeepAliveTimer();

            byte[] bytes = new byte[_data.Length + 3];

            //Add message type
            bytes[0] = _send_option;

            //Add reliable ID
            AttachReliableID(bytes, 1, _ack_callback);

            //Copy data into new array
            Buffer.BlockCopy(_data, 0, bytes, bytes.Length - _data.Length, _data.Length);

            //Write to connection
            WriteBytesToConnection(bytes, bytes.Length);

            Statistics.LogReliableMessageSent(bytes.Length);
        }

        /// <summary>
        ///     Handles a reliable message being received and invokes the data event.
        /// </summary>
        /// <param name="_reader">The buffer received.</param>
        private void ReliableMessageReceive(MessageReader _reader)
        {
            ushort id;
            if (ProcessReliableReceive(_reader.Buffer, 1, out id))
            {
                InvokeDataReceived(_reader);
            }
            else
            {
                _reader.Recycle();
            }
        }

        /// <summary>
        ///     Handles receives from reliable packets.
        /// </summary>
        /// <param name="_bytes">The buffer containing the data.</param>
        /// <param name="_offset">The offset of the reliable header.</param>
        /// <returns>Whether the packet was a new packet or not.</returns>
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
                            reliable_data_packets_missing.Add(i);
                        }
                    }
                    else
                    {
                        int cnt = (ushort.MaxValue - reliable_receive_last) + _id;
                        for (ushort i = 1; i <= cnt; ++i)
                        {
                            reliable_data_packets_missing.Add((ushort)(i + reliable_receive_last));
                        }
                    }

                    //Update the most recently received
                    reliable_receive_last = _id;
                }
                
                //Else it could be a missing packet
                else
                {
                    //See if we're missing it, else this packet is a duplicate as so we return false
                    if (!reliable_data_packets_missing.Remove(_id))
                    {
                        result = false;
                    }
                }
            }

            // Send an acknowledgement
            SendAck(_id);

            return result;
        }

        /// <summary>
        ///     Handles acknowledgement packets to us.
        /// </summary>
        /// <param name="_bytes">The buffer containing the data.</param>
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

                lock (ping_lock)
                {
                    ping_ms = ping_ms * .7f + rt * .3f;
                }
            }
            else if (active_pings.TryFindPing(_id, out DateTime pingPkt))
            {
                Statistics.LogReliablePacketAcknowledged();
                float rt = (float)(DateTime.UtcNow - pingPkt).TotalMilliseconds;

                lock (ping_lock)
                {
                    ping_ms = ping_ms * .7f + rt * .3f;
                }
            }
        }

        /// <summary>
        ///     Sends an acknowledgement for a packet given its identification bytes.
        /// </summary>
        /// <param name="byte1">The first identification byte.</param>
        /// <param name="byte2">The second identification byte.</param>
        private void SendAck(ushort _id)
        {
            byte recent_packets = 0;
            lock (reliable_data_packets_missing)
            {
                for (int i = 1; i <= 8; ++i)
                {
                    if (!reliable_data_packets_missing.Contains((ushort)(_id - i)))
                    {
                        recent_packets |= (byte)(1 << (i - 1));
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
            foreach (var id_packet in reliable_data_packets_sent)
            {
                ushort id = id_packet.Key;
                if (reliable_data_packets_sent.TryRemove(id, out var packet))
                {
                    packet.Recycle();
                }
            }
        }
    }
}
