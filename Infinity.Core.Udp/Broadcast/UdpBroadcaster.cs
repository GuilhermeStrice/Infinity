using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;

namespace Infinity.Core.Udp.Broadcast
{
    public class UdpBroadcaster : IDisposable
    {
        private ConcurrentDictionary<IPEndPoint, Socket> availableAddresses = new ConcurrentDictionary<IPEndPoint, Socket>();
        private byte[] identifier;
        private ILogger logger;

        public UdpBroadcaster(int port, byte[] identifier, ILogger logger = null)
        {
            if (identifier == null || identifier.Length == 0)
            {
                throw new ArgumentException("UdpBroadcaster: identifier can't be null and it's length must be greater than 0");
            }

            this.identifier = identifier;
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

        public void Broadcast(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data argument must not be null and it's length must be greater than 0");
            }

            int data_length = data.Length;
            int identifier_length = identifier.Length;

            var buffer = new byte[identifier_length + data_length];

            Array.Copy(identifier, 0, buffer, 0, identifier_length);
            Array.Copy(data, 0, buffer, identifier_length, data_length);

            foreach (var available_addr in availableAddresses)
            {
                try
                {
                    Socket socket = available_addr.Value;
                    IPEndPoint address = available_addr.Key;
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
