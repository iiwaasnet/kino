using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rawf.Framework
{
    public class ExpirableItemScheduledCollection<T> : IExpirableItemCollection<T>
    {
        private readonly BlockingCollection<ExpirableItem<T>> additionQueue;
        private readonly List<ExpirableItem<T>> delayedItems;
        private readonly Task delayItems;
        private readonly CancellationTokenSource tokenSource;
        private Action<T> handler;

        public ExpirableItemScheduledCollection(TimeSpan evaluationInterval)
        {
            delayedItems = new List<ExpirableItem<T>>();
            tokenSource = new CancellationTokenSource();
            additionQueue = new BlockingCollection<ExpirableItem<T>>(new ConcurrentQueue<ExpirableItem<T>>());
            delayItems = Task.Factory.StartNew(_ => EvaluateDelays(tokenSource.Token, evaluationInterval), tokenSource.Token, TaskCreationOptions.LongRunning);
        }

        public void SetExpirationHandler(Action<T> handler)
        {
            this.handler = handler;
        }

        public void Dispose()
        {
            tokenSource.Cancel(true);
            additionQueue.CompleteAdding();
            delayItems.Wait();
            delayItems.Dispose();
            tokenSource.Dispose();
        }

        public IExpirableItem Delay(T item, TimeSpan expireAfter)
        {
            var delayedItem = new ExpirableItem<T>(item, expireAfter);
            additionQueue.Add(delayedItem);

            return delayedItem;
        }

        private void EvaluateDelays(CancellationToken token, TimeSpan evaluationInterval)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (ContinueRunning(token, evaluationInterval))
                    {
                        AddEnqueuedItems();
                        DeleteExpiredItems();
                    }
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

        private bool ContinueRunning(CancellationToken token, TimeSpan evaluationInterval)
            => WaitHandle.WaitAny(new[] {token.WaitHandle}, evaluationInterval) == WaitHandle.WaitTimeout;

        private void DeleteExpiredItems()
        {
            var now = DateTime.UtcNow;
            while (delayedItems.Count > 0 && delayedItems[0].IsExpired(now))
            {
                if (handler != null)
                {
                    SafeNotifySubscriber(delayedItems[0].Item);
                }
                delayedItems.RemoveAt(0);
            }
        }

        private void AddEnqueuedItems()
        {
            var itemsAdded = false;
            ExpirableItem<T> item;
            while (additionQueue.TryTake(out item))
            {
                delayedItems.Add(item);
                itemsAdded = true;
            }

            if (itemsAdded)
            {
                delayedItems.Sort(Comparison);
            }
        }

        private void SafeNotifySubscriber(T item)
        {
            try
            {
                handler(item);
            }
            catch (Exception err)
            {
                Console.WriteLine(err);
            }
        }

        private int Comparison(ExpirableItem<T> expirableItem, ExpirableItem<T> item)
            => expirableItem.ExpireAfter.CompareTo(item.ExpireAfter);
    }
}