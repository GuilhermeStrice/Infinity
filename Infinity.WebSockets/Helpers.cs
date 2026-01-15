using Infinity.Core;

namespace Infinity.WebSockets
{
    internal static class Helpers
    {
        internal static MessageReader BuildEmptyReader(WebSocketConnection connection)
        {
            MessageReader reader = new MessageReader(connection.allocator);
            return reader;
        }
    }
}