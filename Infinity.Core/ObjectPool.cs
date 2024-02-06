using System.Collections.Concurrent;

namespace Infinity.Core
{
    public sealed class ObjectPool<T> where T : IRecyclable
    {
        public int InUse => pool.Count;

        private ConcurrentStack<T> pool;

        private Func<T> object_factory;
            
        public ObjectPool(Func<T> object_factory)
        {
            this.object_factory = object_factory;

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
            if (!pool.Contains(item))
            {
                pool.Push(item);
            }
        }
    }
}
