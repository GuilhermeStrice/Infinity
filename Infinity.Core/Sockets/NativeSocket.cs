namespace Infinity.Core.Sockets
{
    public class NativeSocket
    {
        private INativeSocket inner_native_socket;

        public NativeSocket()
        {

        }

        public static bool IPv6Support { get; internal set; }

        internal static void InitializeSockets()
        {

        }
    }
}
