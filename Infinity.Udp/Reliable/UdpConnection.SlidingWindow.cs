using Infinity.Core;

namespace Infinity.Udp
{
    public partial class UdpConnection
    {
        /// <summary>
        ///     Packet ids that have not been received, but are expected. 
        /// </summary>
        protected bool[] reliable_data_packets_missing = new bool[ushort.MaxValue + 1];

        protected volatile ushort reliable_receive_last = ushort.MaxValue;

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
    }
}
