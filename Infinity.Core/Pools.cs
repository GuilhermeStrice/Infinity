namespace Infinity.Core
{
    internal static class Pools
    {
        public static readonly ObjectPool<MessageReader> ReaderPool = new ObjectPool<MessageReader>(() => new MessageReader(64000));
        public static readonly ObjectPool<MessageWriter> WriterPool = new ObjectPool<MessageWriter>(() => new MessageWriter(64000));
    }
}
