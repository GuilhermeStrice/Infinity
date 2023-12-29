namespace Infinity.Core
{
    public sealed class ObjectPool<T> where T : IRecyclable
    {
        public int InUse { get; internal set; }
        public int MaxNumberObjects;

        private readonly T[] pool;

        private readonly Func<T> objectFactory;
        
        public ObjectPool(Func<T> objectFactory, int maxNumberObjects = 10000)
        {
            this.objectFactory = objectFactory;

            MaxNumberObjects = maxNumberObjects;
            pool = new T[maxNumberObjects];
        }

        public T GetObject()
        {
            if (InUse > 0)
            {
                lock (pool)
                {
                    return pool[--InUse];
                }
            }
            else
            {
                return objectFactory.Invoke();
            }
        }

        public void PutObject(T item)
        {
            if (InUse < MaxNumberObjects)
            {
                lock (pool)
                {
                    pool[InUse++] = item;
                }
            }
        }
    }
}
