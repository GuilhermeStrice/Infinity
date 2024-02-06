using Infinity.Udp;
using System.Net;
using Infinity.Core;

namespace TestListener
{
    public static class Program
    {
        static volatile bool run = true;
        static volatile int connection_count = 0;

        static void Main(string[] args)
        {
            var ep = new IPEndPoint(IPAddress.Loopback, 22023);
            UdpConnectionListener listener = new UdpConnectionListener(ep, IPMode.IPv4);
            listener.NewConnection += Listener_NewConnection;

            listener.Start();

            while (run)
            {
                if (connection_count >= 50)
                {
                    run = false;
                    Console.WriteLine(listener.ConnectionCount);
                }
            }

            Console.ReadKey();
        }

        static void Listener_NewConnection(NewConnectionEvent new_con)
        {
            new_con.Connection.DataReceived += Connection_DataReceived;
            new_con.Connection.Disconnected += Connection_Disconnected;
            new_con.Recycle();
            connection_count++;
        }

        private static void Connection_Disconnected(DisconnectedEvent obj)
        {
            obj.Recycle();
        }

        private static void Connection_DataReceived(DataReceivedEvent obj)
        {
            obj.Recycle();
        }
    }
}