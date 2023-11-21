using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;

namespace Infinity.Core.Udp.Broadcast
{
    public class UdpBroadcaster : IDisposable
    {
        private ConcurrentDictionary<IPEndPoint, Socket> availableAddresses;
        private byte[] data;
        private ILogger logger;

        public UdpBroadcaster(int port, ILogger logger = null)
        {
            availableAddresses = new ConcurrentDictionary<IPEndPoint, Socket>();

            this.logger = logger;

            var addresses = Util.GetAddressesFromNetworkInterfaces(AddressFamily.InterNetwork);

            if (addresses.Count > 0)
            {
                foreach (var addressInformation in addresses)
                {
                    Socket socket = CreateSocket(new IPEndPoint(addressInformation.Address, 0));
                    IPAddress broadcast = Util.GetBroadcastAddress(addressInformation);

                    availableAddresses.TryAdd(new IPEndPoint(broadcast, port), socket);
                }
            }
            else
            {
                Socket socket = CreateSocket(new IPEndPoint(IPAddress.Any, 0));
                availableAddresses.TryAdd(new IPEndPoint(IPAddress.Broadcast, port), socket);
            }
        }

        private static Socket CreateSocket(IPEndPoint endPoint)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            socket.Bind(endPoint);

            return socket;
        }

        public void SetData(string data)
        {
            int len = Encoding.UTF8.GetByteCount(data);
            this.data = new byte[len + 2];
            this.data[0] = 4;
            this.data[1] = 2;

            Encoding.UTF8.GetBytes(data, 0, data.Length, this.data, 2);
        }

        public void Broadcast()
        {
            if (data == null)
            {
                return;
            }

            foreach (var aa in availableAddresses)
            {
                try
                {
                    Socket socket = aa.Value;
                    IPEndPoint address = aa.Key;
                    socket.BeginSendTo(data, 0, data.Length, SocketFlags.None, address, FinishSendTo, socket);
                }
                catch (Exception e)
                {
                    logger?.WriteError("Broadcaster: " + e);
                }
            }
        }

        private void FinishSendTo(IAsyncResult evt)
        {
            try
            {
                Socket socket = (Socket)evt.AsyncState;
                socket.EndSendTo(evt);
            }
            catch (Exception e)
            {
                logger?.WriteError("Broadcaster: " + e);
            }
        }

        private void CloseSocket(Socket s)
        {
            if (s != null)
            {
                try { s.Shutdown(SocketShutdown.Both); } catch { }
                try { s.Close(); } catch { }
                try { s.Dispose(); } catch { }
            }
        }

        public void Dispose()
        {
            foreach (var aa in availableAddresses)
            {
                Socket socket = aa.Value;
                CloseSocket(socket);
            }

            availableAddresses.Clear();
        }
    }
}
