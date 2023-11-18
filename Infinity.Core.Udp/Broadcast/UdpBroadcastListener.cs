using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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

    public class UdpBroadcastListener : IDisposable
    {
        private Socket socket;
        private EndPoint endpoint;
        private Action<string> logger;

        private byte[] buffer = new byte[1024];

        private List<BroadcastPacket> packets = new List<BroadcastPacket>();

        public bool Running { get; private set; }

        public UdpBroadcastListener(int port, Action<string> logger = null)
        {
            this.logger = logger;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            endpoint = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(endpoint);
        }

        public void StartListen()
        {
            if (Running) return;
            Running = true;

            try
            {
                EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);
                socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref endpt, HandleData, null);
            }
            catch (NullReferenceException) { }
            catch (Exception e)
            {
                logger?.Invoke("BroadcastListener: " + e);
                Dispose();
            }
        }

        private void HandleData(IAsyncResult result)
        {
            Running = false;

            int numBytes;
            EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                numBytes = socket.EndReceiveFrom(result, ref endpt);
            }
            catch (NullReferenceException)
            {
                // Already disposed
                return;
            }
            catch (Exception e)
            {
                logger?.Invoke("BroadcastListener: " + e);
                Dispose();
                return;
            }

            if (numBytes < 3
                || buffer[0] != 4 || buffer[1] != 2)
            {
                StartListen();
                return;
            }

            IPEndPoint ipEnd = (IPEndPoint)endpt;
            string data = Encoding.UTF8.GetString(buffer, 2, numBytes - 2);
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
