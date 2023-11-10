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
        private int numberCreated;
        public int NumberCreated { get { return numberCreated; } }

        public int NumberInUse { get { return inUse.Count; } }
        public int NumberNotInUse { get { return pool.Count; } }
        public int Size { get { return NumberInUse + NumberNotInUse; } }

        private readonly List<T> pool = new List<T>();

        // Unavailable objects
        private readonly ConcurrentDictionary<T, bool> inUse = new ConcurrentDictionary<T, bool>();

        /// <summary>
        ///     The generator for creating new objects.
        /// </summary>
        /// <returns></returns>
        private readonly Func<T> objectFactory;
        
        /// <summary>
        ///     public constructor for our ObjectPool.
        /// </summary>
        public ObjectPool(Func<T> objectFactory)
        {
            this.objectFactory = objectFactory;
        }

        /// <summary>
        ///     Returns a pooled object of type T, if none are available another is created.
        /// </summary>
        /// <returns>An instance of T.</returns>
        public T GetObject()
        {
            T item;
            lock (pool)
            {
                if (pool.Count > 0)
                {
                    var idx = pool.Count - 1;
                    item = pool[idx];
                    pool.RemoveAt(idx);
                }
                else
                {
                    Interlocked.Increment(ref numberCreated);
                    item = objectFactory.Invoke();
                }
            }

            if (!inUse.TryAdd(item, true))
            {
                throw new Exception("Duplicate pull " + typeof(T).Name);
            }

            return item;
        }

        /// <summary>
        ///     Returns an object to the pool.
        /// </summary>
        /// <param name="item">The item to return.</param>
        public void PutObject(T item)
        {
            if (inUse.TryRemove(item, out bool b))
            {
                lock (pool)
                {
                    pool.Add(item);
                }
            }
            else
            {
#if DEBUG
                throw new Exception("Duplicate add " + typeof(T).Name);
#endif
            }
        }
    }
}
