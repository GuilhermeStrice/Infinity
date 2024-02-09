namespace Infinity.Core
{
    public class FastConcurrentDictionary<K, V> : Dictionary<K, V> where K : notnull
    {
        private readonly object @lock = new object();

        public new V this[K key]
        {
            get
            {
                lock (@lock)
                {
                    return base[key];
                }
            }

            set
            {
                lock (@lock)
                {
                    base[key] = value;
                }
            }
        }

        public new int Count
        {
            get
            {
                lock (@lock)
                {
                    return base.Count;
                }
            }
        }

        public new bool TryAdd(K key, V value)
        {
            lock (@lock)
            {
                return base.TryAdd(key, value);
            }
        }

        public new void Add(K key, V value)
        {
            lock (@lock)
            {
                base.Add(key, value);
            }
        }

        public new bool ContainsKey(K key)
        {
            lock (@lock)
            {
                return base.ContainsKey(key);
            }
        }

        public new bool TryGetValue(K key, out V value)
        {
            lock (@lock)
            {
                return base.TryGetValue(key, out value);
            }
        }

        public bool TryRemove(K key, out V value)
        {
            lock (@lock)
            {
                return Remove(key, out value);
            }
        }

        public new bool Remove(K key)
        {
            lock (@lock)
            {
                return base.Remove(key);
            }
        }

        public new void Clear()
        {
            lock (@lock)
            {
                base.Clear();
            }
        }

        public void ForEach(Action<KeyValuePair<K, V>> action)
        {
            lock (@lock)
            {
                foreach (var kv in this)
                {
                    action(kv);
                }
            }
        }
    }
}
