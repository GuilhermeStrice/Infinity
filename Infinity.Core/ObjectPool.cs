using System.Collections.Concurrent;

namespace Infinity.Core
{
    /// <summary>
    ///     A fairly simple object pool for items that will be created a lot.
    /// </summary>
    /// <typeparam name="T">The type that is pooled.</typeparam>
    /// <threadsafety static="true" instance="true"/>
    public sealed class ObjectPool<T> where T : IRecyclable
    {
        public int InUse => MaxNumberObjects - instance_count;
        public int MaxNumberObjects;

        private readonly T[] pool;
        private int instance_count;

        /// <summary>
        ///     The generator for creating new objects.
        /// </summary>
        /// <returns></returns>
        private readonly Func<T> objectFactory;
        
        /// <summary>
        ///     public constructor for our ObjectPool.
        /// </summary>
        public ObjectPool(Func<T> objectFactory, int maxNumberObjects = 5000)
        {
            this.objectFactory = objectFactory;

            MaxNumberObjects = maxNumberObjects;
            pool = new T[maxNumberObjects];
        }

        /// <summary>
        ///     Returns a pooled object of type T, if none are available another is created.
        /// </summary>
        /// <returns>An instance of T.</returns>
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

        /// <summary>
        ///     Returns an object to the pool.
        /// </summary>
        /// <param name="item">The item to return.</param>
        public void PutObject(T item)
        {
            if (instance_count < MaxNumberObjects)
            {
                pool[instance_count++] = item;
            }
        }
    }
}
