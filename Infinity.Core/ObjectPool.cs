using System.Collections.Concurrent;

namespace Infinity.Core
{
    public sealed class ObjectPool<T> where T : IRecyclable
    {
        public int InUse { get; internal set; }
        public int MaxNumberObjects;

        private readonly ConcurrentStack<T> pool;

        private readonly Func<T> object_factory;
        
        public ObjectPool(Func<T> object_factory, int maxNumberObjects = 10000)
        {
            this.object_factory = object_factory;

            MaxNumberObjects = maxNumberObjects;
            pool = new ConcurrentStack<T>();
        }

        public T GetObject()
        {
            T item;
            if (pool.TryPop(out item))
            {
                return item;
            }

            return object_factory();
        }

        public void PutObject(T item)
        {
            pool.Push(item);
        }
    }
}
