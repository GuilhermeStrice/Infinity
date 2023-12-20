using System.Collections.Concurrent;

namespace Infinity.Core
{
    public sealed class ObjectPool<T> where T : IRecyclable
    {
        public int InUse => MaxNumberObjects - instance_count;
        public int MaxNumberObjects;

        private readonly T[] pool;
        private int instance_count;

        private readonly Func<T> objectFactory;
        
        public ObjectPool(Func<T> objectFactory, int maxNumberObjects = 10000)
        {
            this.objectFactory = objectFactory;

            MaxNumberObjects = maxNumberObjects;
            pool = new T[maxNumberObjects];
        }

        public T GetObject()
        {
            if (instance_count > 0)
            {
                return pool[--instance_count];
            }
            else
            {
                return objectFactory.Invoke();
            }
        }

        public void PutObject(T item)
        {
            if (instance_count < MaxNumberObjects)
            {
                pool[instance_count++] = item;
            }
        }
    }
}
