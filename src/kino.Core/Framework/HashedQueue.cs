using System;
using System.Collections.Generic;
using System.Linq;
using C5;

namespace kino.Core.Framework
{
    public class HashedQueue<T>
    {
        private readonly int maxQueueLength;
        private readonly HashedLinkedList<T> collection;
        private readonly object @lock = new object();

        public HashedQueue(int maxQueueLength)
        {
            this.maxQueueLength = maxQueueLength;
            collection = new HashedLinkedList<T>();
        }

        public HashedQueue()
            : this(int.MaxValue)
        {
        }

        public bool TryEnqueue(T item)
        {
            lock (@lock)
            {
                if (collection.Count < maxQueueLength && !collection.Contains(item))
                {
                    collection.InsertLast(item);
                    return true;
                }
            }

            return false;
        }

        public bool TryPeek(out System.Collections.Generic.IList<T> items, int count)
        {
            items = new List<T>();

            lock (@lock)
            {
                var dequeueCount = Math.Min(collection.Count, count);
                ((List<T>)items).AddRange(collection.View(0, dequeueCount));
            }

            return items.Any();
        }

        public void TryDelete(IEnumerable<T> items)
        {
            lock (@lock)
            {
                collection.RemoveAll(items);
            }
        }

        public void TryDelete(T item)
        {
            lock (@lock)
            {
                collection.Remove(item);
            }
        }
    }
}