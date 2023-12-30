using System.Collections.Concurrent;

namespace Infinity.Core
{
    public sealed class ObjectPool<T> where T : IRecyclable
    {
        public int InUse => in_use;
        public int MaxNumberObjects;

        private ConcurrentStack<T> pool;

        private Func<T> object_factory;

        private volatile int in_use = 0;
            
        public ObjectPool(Func<T> object_factory, int maxNumberObjects = 10000)
        {
            this.object_factory = object_factory;

            MaxNumberObjects = maxNumberObjects;
            pool = new ConcurrentStack<T>();
        }

        public T GetObject()
        {
            Interlocked.Increment(ref in_use);
            T item;
            if (pool.TryPop(out item))
            {
                return item;
            }

            return object_factory();
        }

        public void PutObject(T item)
        {
            if (!pool.Contains(item))
            {
                Interlocked.Decrement(ref in_use);
                pool.Push(item);
            }
        }
    }
}
