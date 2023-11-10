using System.Net;
using Infinity.Core;

namespace Infinity.Server.Console
{
    static class Program
    {
        static void Main(string[] args)
        {
            /*UdpConnectionListener listener = new UdpConnectionListener(new IPEndPoint(IPAddress.Any, 2678));
            listener.Start();

            listener.NewConnection += Listener_NewConnection;
            listener.OnInternalError += Listener_OnInternalError;

            System.Console.ReadKey();*/
        }

        /*private static void Listener_OnInternalError(InfinityInternalErrors obj)
        {
            System.Console.WriteLine("internal error" + obj.ToString());
        }

        private static void Listener_NewConnection(NewConnectionEventArgs obj)
        {
            System.Console.WriteLine("this guy connected");
            obj.Connection.DataReceived += Connection_DataReceived;
            obj.Connection.Disconnected += Connection_Disconnected;

            // this works, i dont know why
            Thread.Sleep(1000);
            MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
            messageWriter.Write("pong");
            obj.Connection.Send(messageWriter);
            messageWriter.Recycle();
        }

        private static void Connection_Disconnected(object? sender, DisconnectedEventArgs e)
        {
            System.Console.WriteLine("disconnected");
        }

        private static void Connection_DataReceived(DataReceivedEventArgs obj)
        {
            var ping = obj.Message.ReadString();
            if (ping == "ping")
            {
                System.Console.WriteLine("ping");
                MessageWriter messageWriter = MessageWriter.Get(SendOption.Reliable);
                messageWriter.Write("pong");
                obj.Sender.Send(messageWriter);
                messageWriter.Recycle();
            }

            obj.Message.Recycle();
        }*/
    }
}