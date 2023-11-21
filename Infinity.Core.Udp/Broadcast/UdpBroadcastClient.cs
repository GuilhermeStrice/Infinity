using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Infinity.Core.Udp.Broadcast
{
    public class BroadcastPacket
    {
        public string Data;
        public DateTime ReceiveTime;
        public IPEndPoint Sender;

        public BroadcastPacket(string data, IPEndPoint sender)
        {
            Data = data;
            Sender = sender;
            ReceiveTime = DateTime.Now;
        }

        public string GetAddress()
        {
            return Sender.Address.ToString();
        }
    }

    public class UdpBroadcastClient : IDisposable
    {
        private Socket socket;
        private EndPoint endpoint;
        private ILogger logger;

        private byte[] buffer = new byte[8086];

        private List<BroadcastPacket> packets = new List<BroadcastPacket>();

        private byte[] identifier;

        public bool Running { get; private set; }

        public UdpBroadcastClient(int port, ILogger logger = null)
        {
            this.logger = logger;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            endpoint = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(endpoint);
        }

        public void SetIdentifier(byte[] identifier)
        {
            this.identifier = identifier;
        }

        public void StartListen()
        {
            if (Running)
                return;
            
            Running = true;

            try
            {
                EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);
                socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref endpt, HandleData, null);
            }
            catch (NullReferenceException) { }
            catch (Exception e)
            {
                logger?.WriteError("BroadcastListener: " + e);
                Dispose();
            }
        }

        private void HandleData(IAsyncResult result)
        {
            Running = false;

            int len;
            EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                len = socket.EndReceiveFrom(result, ref endpt);
            }
            catch (NullReferenceException)
            {
                // Already disposed
                return;
            }
            catch (Exception e)
            {
                logger?.WriteError("BroadcastListener: " + e);
                Dispose();
                return;
            }

            int ident_len = identifier.Count();

            // if its equals to the length of identifier it means there's no data
            if (len > ident_len)
            {
                for (int i = 0; i < ident_len; i++)
                {
                    if (buffer[i] != identifier[i])
                    {
                        StartListen();
                        return;
                    }
                }

                IPEndPoint ipEnd = (IPEndPoint)endpt;
                string data = Encoding.UTF8.GetString(buffer, ident_len, len - ident_len);
                int dataHash = data.GetHashCode();

                lock (packets)
                {
                    bool found = false;
                    for (int i = 0; i < packets.Count; ++i)
                    {
                        var pkt = packets[i];

                        if (pkt == null || pkt.Data == null)
                        {
                            packets.RemoveAt(i);
                            i--;
                            continue;
                        }

                        if (pkt.Data.GetHashCode() == dataHash && pkt.Sender.Equals(ipEnd))
                        {
                            packets[i].ReceiveTime = DateTime.Now;
                            break;
                        }
                    }

                    if (!found)
                    {
                        packets.Add(new BroadcastPacket(data, ipEnd));
                    }
                }

                StartListen();
            }
        }

        public BroadcastPacket[] GetPackets()
        {
            lock (packets)
            {
                var output = packets.ToArray();
                packets.Clear();
                return output;
            }
        }

        public void Dispose()
        {
            if (socket != null)
            {
                try { socket.Shutdown(SocketShutdown.Both); } catch { }
                try { socket.Close(); } catch { }
                try { socket.Dispose(); } catch { }
                socket = null;
            }
        }
    }
}
