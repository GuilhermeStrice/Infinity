using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;

namespace Infinity.Core.Udp.Broadcast
{
    public class UdpBroadcastServer
    {
        private ConcurrentDictionary<IPEndPoint, Socket> availableAddresses;
        private byte[] identifier;
        private ILogger logger;

        public UdpBroadcastServer(int port, byte[] identifier, ILogger logger = null)
        {
            if (identifier == null)
                throw new UdpBroadcastException("identifier can't be null");

            this.identifier = identifier;

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

        public void Broadcast(string data)
        {
            int data_length = Encoding.UTF8.GetByteCount(data);
            int identifier_length = identifier.Count();

            var buffer = new byte[identifier_length + data_length];

            Array.Copy(identifier, 0, buffer, 0, identifier_length);
            Encoding.UTF8.GetBytes(data, 0, data.Length, buffer, identifier_length);

            if (data == null)
            {
                throw new UdpBroadcastException("Set some data please");
            }

            foreach (var aa in availableAddresses)
            {
                try
                {
                    Socket socket = aa.Value;
                    IPEndPoint address = aa.Key;
                    socket.BeginSendTo(buffer, 0, buffer.Length, SocketFlags.None, address, FinishSendTo, socket);
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

        public void Stop()
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
