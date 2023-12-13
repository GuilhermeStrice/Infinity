﻿using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Collections.Concurrent;

namespace Infinity.Core.Udp.Broadcast
{
    public class UdpBroadcaster : IDisposable
    {
        private ConcurrentDictionary<IPEndPoint, Socket> available_addresses = new ConcurrentDictionary<IPEndPoint, Socket>();
        private byte[] identifier;
        private ILogger logger;

        public UdpBroadcaster(int _port, byte[] _identifier, ILogger _logger = null)
        {
            if (_identifier == null || _identifier.Length == 0)
            {
                throw new ArgumentException("UdpBroadcaster: identifier can't be null and it's length must be greater than 0");
            }

            identifier = _identifier;
            logger = _logger;

            var addresses = Util.GetAddressesFromNetworkInterfaces(AddressFamily.InterNetwork);

            if (addresses.Count > 0)
            {
                foreach (var addressInformation in addresses)
                {
                    Socket socket = CreateSocket(new IPEndPoint(addressInformation.Address, 0));
                    IPAddress broadcast = Util.GetBroadcastAddress(addressInformation);

                    available_addresses.TryAdd(new IPEndPoint(broadcast, _port), socket);
                }
            }
            else
            {
                Socket socket = CreateSocket(new IPEndPoint(IPAddress.Any, 0));
                available_addresses.TryAdd(new IPEndPoint(IPAddress.Broadcast, _port), socket);
            }
        }

        private static Socket CreateSocket(IPEndPoint _endpoint)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            socket.Bind(_endpoint);

            return socket;
        }

        public void Broadcast(byte[] _data)
        {
            if (_data == null || _data.Length == 0)
            {
                throw new ArgumentException("Data argument must not be null and it's length must be greater than 0");
            }

            int data_length = _data.Length;
            int identifier_length = identifier.Length;

            var buffer = new byte[identifier_length + data_length];

            Array.Copy(identifier, 0, buffer, 0, identifier_length);
            Array.Copy(_data, 0, buffer, identifier_length, data_length);

            foreach (var available_addr in available_addresses)
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

        private void FinishSendTo(IAsyncResult result)
        {
            try
            {
                Socket socket = (Socket)result.AsyncState;
                socket.EndSendTo(result);
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
            foreach (var available_addr in available_addresses)
            {
                Socket socket = available_addr.Value;
                CloseSocket(socket);
            }

            available_addresses.Clear();
        }
    }
}
