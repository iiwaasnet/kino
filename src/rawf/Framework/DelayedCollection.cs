using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rawf.Framework
{
    public class DelayedCollection<T> : IDisposable
    {
        private readonly BlockingCollection<DelayedItem<T>> additionQueue;
        private readonly List<DelayedItem<T>> delayedItems;
        private readonly Task delayItems;
        private readonly CancellationTokenSource tokenSource;

        public DelayedCollection()
        {
            delayedItems = new List<DelayedItem<T>>();
            tokenSource = new CancellationTokenSource();
            additionQueue = new BlockingCollection<DelayedItem<T>>(new ConcurrentQueue<DelayedItem<T>>());
            delayItems = Task.Factory.StartNew(_ => EvaluateDelays(tokenSource.Token), tokenSource.Token, TaskCreationOptions.LongRunning);
        }

        public void Dispose()
        {
            tokenSource.Cancel(true);
            additionQueue.CompleteAdding();
        }

        private void EvaluateDelays(CancellationToken token)
        {
            try
            {
                //var itemAdded = new CancellationTokenSource(Timeout.InfiniteTimeSpan);

                //foreach (var delayedItem in additionQueue.GetConsumingEnumerable(itemAdded.Token))
                //{
                //    delayedItems.Add(delayedItem);
                //    delayedItems.Sort(Comparison);
                //    itemAdded = new CancellationTokenSource(delayedItems[0].ExpireAfter);
                //}
                while (!token.IsCancellationRequested)
                {
                    DelayedItem<T> item;
                    while (additionQueue.TryTake(out item))
                    {
                        delayedItems.Add(item);
                    }
                    var now = DateTime.UtcNow;

                    delayedItems.Sort(Comparison);
                    while (delayedItems.Count > 0 && delayedItems[0].IsExpired())
                    {
                        delayedItems.RemoveAt(0);
                    }
                    var sleep = (delayedItems.Count > 0) ? delayedItems[0].ExpireAfter : Timeout.Infinite;
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
            finally
            {
                additionQueue.Dispose();
            }
        }

        private int Comparison(DelayedItem<T> delayedItem, DelayedItem<T> item)
        {
            return delayedItem.ExpireAfter.CompareTo(item.ExpireAfter);
        }

        public IDelayedItem Delay(T item, TimeSpan expireAfter)
        {
            var delayedItem = new DelayedItem<T>(item, expireAfter, this);
            additionQueue.Add(delayedItem);

            return delayedItem;
        }

        private void TriggerDelayEvaluation()
        {
        }

        private class DelayedItem<T> : IDelayedItem
        {
            private readonly DelayedCollection<T> storage;
            private readonly DateTime timeAdded;

            internal DelayedItem(T item, TimeSpan expireAfter, DelayedCollection<T> storage)
            {
                this.storage = storage;
                Item = item;
                timeAdded = DateTime.UtcNow;
                ExpireAfter = expireAfter.Milliseconds;
            }

            internal T Item { get; }
            internal int ExpireAfter { get; set; }

            public void ExpireNow()
            {
                ExpireAfter = 0;
                storage.TriggerDelayEvaluation();
            }

            internal bool IsExpired()
                => DateTime.UtcNow > timeAdded.AddMilliseconds(ExpireAfter);
        }
    }

    public interface IDelayedItem
    {
        void ExpireNow();
    }
}