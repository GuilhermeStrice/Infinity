namespace Infinity.Core
{
    internal static class Pools
    {
        public static readonly ObjectPool<MessageReader> ReaderPool = new ObjectPool<MessageReader>(() => new MessageReader(MessageReader.BufferSize));
        public static readonly ObjectPool<MessageWriter> WriterPool = new ObjectPool<MessageWriter>(() => new MessageWriter(MessageWriter.BufferSize));
    }
}
