using Infinity.Udp;
using System.Net;
using Infinity.Core;

namespace TestListener
{
    public static class Program
    {
        static volatile int connection_count = 0;
        static volatile int message_count = 0;

        static IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, 22023);

        static UdpConnectionListener listener = new UdpConnectionListener(ep, IPMode.IPv4);

        static void Main(string[] args)
        {
            listener.NewConnection += Listener_NewConnection;

            listener.Start();

            Console.WriteLine("Listening");

            Console.ReadKey();
        }

        static void Listener_NewConnection(NewConnectionEvent new_con)
        {
            new_con.Connection.DataReceived += Connection_DataReceived;
            new_con.Connection.Disconnected += Connection_Disconnected;
            new_con.Recycle();
            connection_count++;

            if (connection_count >= 50)
            {
                Console.WriteLine(listener.ConnectionCount);
            }
        }

        private static async Task Connection_Disconnected(DisconnectedEvent obj)
        {
            obj.Recycle();
        }

        private static async Task Connection_DataReceived(DataReceivedEvent obj)
        {
            message_count++;
            if (message_count == 10000)
            {
                Console.WriteLine("5000");
            }

            obj.Recycle(true);
        }
    }
}