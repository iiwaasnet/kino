using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace kino.Core.Framework
{
    public class ConcurrentHashSet<T> : IProducerConsumerCollection<T>
    {
        private readonly ConcurrentDictionary<T, object> collection;
        private volatile int maxItemsCount = -1;

        public ConcurrentHashSet()
        {
            collection = new ConcurrentDictionary<T, object>();
        }

        public void SetHasSetMaxSize(int maxItemsCount)
            => this.maxItemsCount = maxItemsCount;

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public IEnumerator<T> GetEnumerator()
            => collection.Keys.GetEnumerator();

        public void CopyTo(Array array, int index)
            => ((ICollection) collection.Keys.ToList()).CopyTo(array, index);

        public bool IsSynchronized => false;

        public void CopyTo(T[] array, int index)
            => collection.Keys.ToList().CopyTo(array, index);

        public bool TryAdd(T item)
        {
            if (collection.Count < maxItemsCount)
            {
                collection.TryAdd(item, null);
            }

            return true;
        }

        public bool TryTake(out T item)
        {
            object _;
            item = collection.Keys.FirstOrDefault();
            return !EqualityComparer<T>.Default.Equals(item, default(T)) && collection.TryRemove(item, out _);
        }

        public T[] ToArray()
            => collection.Keys.ToArray();

        public int Count => collection.Count;

        public object SyncRoot
        {
            get { throw new NotSupportedException(); }
        }
    }
}