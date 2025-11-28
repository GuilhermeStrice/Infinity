namespace Infinity.Core
{
    public static class Pools
    {
        public static int DefaultBufferSize = 64000;

        public static ObjectPool<MessageReader> ReaderPool = new ObjectPool<MessageReader>(() => new MessageReader(DefaultBufferSize));
        public static ObjectPool<MessageWriter> WriterPool = new ObjectPool<MessageWriter>(() => new MessageWriter(DefaultBufferSize));

        public static ObjectPool<DataReceivedEvent> DataReceivedEventPool = new ObjectPool<DataReceivedEvent>(() => new DataReceivedEvent());
        public static ObjectPool<DisconnectedEvent> DisconnectedEventPool = new ObjectPool<DisconnectedEvent>(() => new DisconnectedEvent());
        public static ObjectPool<NewConnectionEvent> NewConnectionPool = new ObjectPool<NewConnectionEvent>(() => new NewConnectionEvent());
    }
}
