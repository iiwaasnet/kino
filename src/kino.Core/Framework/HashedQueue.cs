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
            :this(int.MaxValue)
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

        public bool TryDequeue(out T item)
        {
            item = default(T);

            lock (@lock)
            {
                if (collection.Count > 0)
                {
                    item = collection.RemoveFirst();
                    return true;
                }
            }

            return false;
        }

        public bool TryDequeue(out IEnumerable<T> items, int count)
        {
            var tmp = new List<T>(count);
            items = tmp;

            lock (@lock)
            {
                var dequeueCount = Math.Min(collection.Count, count);
                while (dequeueCount-- > 0)
                {
                    tmp.Add(collection.RemoveFirst());
                }
            }

            return items.Any();
        }
    }
}