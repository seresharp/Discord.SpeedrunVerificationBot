using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    public class ConcurrentSet<T> : ICollection<T>, IEnumerable<T>, IEnumerable
    {
        private readonly ConcurrentDictionary<T, byte> Backing = new();

        public static ConcurrentSet<T> CreateNew()
            => new();

        public int Count => Backing.Count;

        public bool IsEmpty => Backing.IsEmpty;

        public bool IsReadOnly => false;

        public void Clear() => Backing.Clear();

        public bool Contains(T item) => Backing.ContainsKey(item);

        public bool TryAdd(T item) => Backing.TryAdd(item, 0);

        public bool TryRemove(T item) => Backing.TryRemove(item, out _);

        public void Add(T item) => TryAdd(item);

        public bool Remove(T item) => TryRemove(item);

        public void CopyTo(T[] array, int arrayIndex) => Backing.Keys.CopyTo(array, arrayIndex);

        public IEnumerator<T> GetEnumerator()
        {
            foreach ((T val, _) in Backing)
            {
                yield return val;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
