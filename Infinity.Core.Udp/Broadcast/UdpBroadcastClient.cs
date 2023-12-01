using System.Net.Sockets;
using System.Net;
using System.Text;

namespace Infinity.Core.Udp.Broadcast
{
    public delegate void OnBroadcastReceive(byte[] data, IPEndPoint sender);

    public class UdpBroadcastClient : IDisposable
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

        public event OnBroadcastReceive OnBroadcastReceive;

        public UdpBroadcastClient(int port, byte[] identifier, ILogger logger = null)
        {
            if (identifier == null)
                throw new UdpBroadcastException("identifier can't be null");

            this.identifier = identifier;

            this.logger = logger;
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.EnableBroadcast = true;
            socket.MulticastLoopback = false;
            endpoint = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(endpoint);
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
            Running = false;

            int len = 0;
            EndPoint endpt = new IPEndPoint(IPAddress.Any, 0);

            try
            {
                len = socket.EndReceiveFrom(result, ref endpt);
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

            var identifier_len = identifier.Count();

            // if its equals to the length of identifier it means there's no data
            if (len > identifier_len)
            {
                for (int i = 0; i < identifier_len; i++)
                {
                    if (buffer[i] != identifier[i])
                    {
                        StartListen();
                        return;
                    }
                }

                var ipEnd = (IPEndPoint)endpt;
                var data = new byte[len - identifier_len];
                Array.Copy(buffer, identifier_len, data, 0, len - identifier_len);

                OnBroadcastReceive?.Invoke(data, ipEnd);
            }

            // Since this is an async operation we don't really care if we sleep here
            // it's up to the user to specify a sane time frame
            Thread.Sleep(PollTime);
            StartListen();
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
