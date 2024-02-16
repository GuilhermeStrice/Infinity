namespace Infinity.Core
{
    public class FastConcurrentDictionary<K, V>
        where K : notnull
        where V : notnull
    {
        private Dictionary<K, V> inner_dictionary = new Dictionary<K, V>();
        private readonly object @lock = new object();

        public V this[K key]
        {
            get
            {
                lock (@lock)
                {
                    return inner_dictionary[key];
                }
            }

            set
            {
                lock (@lock)
                {
                    inner_dictionary[key] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (@lock)
                {
                    return inner_dictionary.Count;
                }
            }
        }

        public bool TryAdd(K key, V value)
        {
            lock (@lock)
            {
                return inner_dictionary.TryAdd(key, value);
            }
        }

        public void Add(K key, V value)
        {
            lock (@lock)
            {
                inner_dictionary.Add(key, value);
            }
        }

        public bool ContainsKey(K key)
        {
            lock (@lock)
            {
                return inner_dictionary.ContainsKey(key);
            }
        }

        public bool TryGetValue(K key, out V value)
        {
            lock (@lock)
            {
                return inner_dictionary.TryGetValue(key, out value);
            }
        }

        public bool TryRemove(K key, out V value)
        {
            lock (@lock)
            {
                return inner_dictionary.Remove(key, out value);
            }
        }

        public bool Remove(K key)
        {
            lock (@lock)
            {
                return inner_dictionary.Remove(key);
            }
        }

        public void Clear()
        {
            lock (@lock)
            {
                inner_dictionary.Clear();
            }
        }

        public void ForEach(Action<KeyValuePair<K, V>> action)
        {
            lock (@lock)
            {
                foreach (var kv in inner_dictionary)
                {
                    action(kv);
                }
            }
        }
    }
}
