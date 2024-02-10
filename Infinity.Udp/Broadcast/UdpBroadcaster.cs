using System.Net.Sockets;
using System.Net;
using Infinity.Core;
using Infinity.Core.Net;
using Infinity.Core.Net.Sockets.Native;
using System.Runtime.InteropServices;
using Infinity.Core.Net.Sockets.Native.Win32;
using System.Runtime.CompilerServices;
using System.Net.NetworkInformation;

namespace Infinity.Udp.Broadcast
{
    public class UdpBroadcaster : IDisposable
    {
        private FastConcurrentDictionary<IPEndPoint, Socket> available_addresses = new FastConcurrentDictionary<IPEndPoint, Socket>();
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

            nint addresses = Util.getaddrinfo(Core.Net.Sockets.AddressFamily.InterNetwork);

            if (addresses != 0)
            {
                var ptr = addresses;
                while (ptr != 0)
                {
                    try
                    {
                        var addr_info = Marshal.PtrToStructure<AddressInfo>(ptr);

                        var addr_ptr = addr_info.ai_addr;
                        var in_addr = Marshal.PtrToStructure<SocketAddressInput>(addr_ptr);
                        var ip = new NativeIPAddress(in_addr.sin_addr.S_addr);
                        var ip_str = ip.ToString();

                        Socket socket = CreateSocket(new IPEndPoint(IPAddress.Parse(ip_str), 0));

                        available_addresses.TryAdd(new IPEndPoint(IPAddress.Parse(ip_str), _port), socket);

                        ptr = addr_info.ai_next;
                    }
                    catch
                    {
                        // this fails on github tests sometimes
                    }
                }

                Util.freeaddrinfo(addresses);
            }
            else
            {
                Socket socket = CreateSocket(new IPEndPoint(IPAddress.Any, 0));
                available_addresses.TryAdd(new IPEndPoint(IPAddress.Broadcast, _port), socket);
            }
        }

        public void Broadcast(byte[] _buffer)
        {
            if (_buffer == null || _buffer.Length == 0)
            {
                throw new ArgumentException("Data argument must not be null and it's length must be greater than 0");
            }

            int data_length = _buffer.Length;
            int identifier_length = identifier.Length;

            var buffer = new byte[identifier_length + data_length];

            Array.Copy(identifier, 0, buffer, 0, identifier_length);
            Array.Copy(_buffer, 0, buffer, identifier_length, data_length);

            available_addresses.ForEach(available_addr =>
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
            });
        }

        public void Dispose()
        {
            available_addresses.ForEach(available_addr =>
            {
                Socket socket = available_addr.Value;
                CloseSocket(socket);
            });

            available_addresses.Clear();
        }

        private static Socket CreateSocket(IPEndPoint _endpoint)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            socket.Bind(_endpoint);

            return socket;
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
    }
}
