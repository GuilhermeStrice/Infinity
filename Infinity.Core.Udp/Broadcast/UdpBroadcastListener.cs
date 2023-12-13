using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Infinity.Core.Udp.Broadcast
{
    public delegate void OnBroadcastReceive(byte[] data, IPEndPoint sender);

    public class UdpBroadcastListener : IDisposable
    {
        private Socket socket;
        private EndPoint endpoint;
        private ILogger logger;

        private byte[] buffer = new byte[8086];
        private byte[] identifier;

        public bool Running { get; private set; }

        /// <summary>
        /// Time to wait between each Broadcast read in milliseconds. Defaults to 1000
        /// </summary>
        public int PollTime { get; set; } = 1000;

        public event OnBroadcastReceive? OnBroadcastReceive;

        public UdpBroadcastListener(int port, byte[] identifier, ILogger logger = null)
        {
            if (identifier == null || identifier.Length == 0)
            {
                throw new ArgumentException("Identifier paramenter must not be null and it's length must be greater than 0");
            }

            this.identifier = identifier;
            this.logger = logger;

            endpoint = new IPEndPoint(IPAddress.Any, port);
            socket = CreateSocket((IPEndPoint)endpoint);
        }

        private static Socket CreateSocket(IPEndPoint endPoint)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            socket.Bind(endPoint);

            return socket;
        }

        public void StartListen()
        {
            if (Running)
            {
                return;
            }
            
            Running = true;

            try
            {
                socket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None, ref endpoint, HandleData, null);
            }
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                logger?.WriteError("BroadcastListener: " + e);
                Dispose();
            }
        }

        private void HandleData(IAsyncResult result)
        {
            EndPoint receive_endpoint = new IPEndPoint(IPAddress.Any, 0);
            int length = 0;

            try
            {
                length = socket.EndReceiveFrom(result, ref receive_endpoint);
            }
            catch (NullReferenceException) { }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception e)
            {
                logger?.WriteError("BroadcastListener: " + e);
                Dispose();
                return;
            }

            var identifier_length = identifier.Count();

            // if its equals to the length of identifier it means there's no data
            if (length > identifier_length)
            {
                for (int i = 0; i < identifier_length; i++)
                {
                    if (buffer[i] != identifier[i])
                    {
                        StartListen();
                        return;
                    }
                }

                var data = new byte[length - identifier_length];
                Array.Copy(buffer, identifier_length, data, 0, length - identifier_length);

                OnBroadcastReceive?.Invoke(data, (IPEndPoint)receive_endpoint);
            }

            // Since this is an async operation we don't really care if we sleep here
            // it's up to the user to specify a sane time frame
            Thread.Sleep(PollTime);
            StartListen();
        }

        public void Dispose()
        {
            Running = false;

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
