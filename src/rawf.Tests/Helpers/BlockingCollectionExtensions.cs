using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace rawf.Tests.Helpers
{
    public static class BlockingCollectionExtensions
    {
        public static T BlockingLast<T>(this IEnumerable<T> collection, TimeSpan timeout)
        {
            var blockingCollection = collection as BlockingCollection<T>;
            if (blockingCollection != null)
            {
                var tmp = new List<T>();
                try
                {
                    using (var cancellationTokenSource = new CancellationTokenSource(timeout))
                    {
                        while (true)
                        {
                            tmp.Add(blockingCollection.Take(cancellationTokenSource.Token));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                }
                return tmp.Last();
            }

            return collection.Last();
        }

        public static T BlockingFirst<T>(this IEnumerable<T> collection)
        {
            collection = (collection as BlockingCollection<T>)?.GetConsumingEnumerable();

            return collection.First();
        }
    }
}