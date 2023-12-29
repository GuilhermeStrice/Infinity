namespace Infinity.Core
{
    public static class Pools
    {
        public static ObjectPool<MessageReader> ReaderPool = new ObjectPool<MessageReader>(() => new MessageReader(64000));
        public static ObjectPool<MessageWriter> WriterPool = new ObjectPool<MessageWriter>(() => new MessageWriter(64000));
    }
}
