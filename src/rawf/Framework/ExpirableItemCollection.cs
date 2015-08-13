using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rawf.Framework
{
    public partial class ExpirableItemCollection<T> : IExpirableItemCollection<T>
    {
        private readonly BlockingCollection<ExpirableItem> additionQueue;
        private readonly List<ExpirableItem> delayedItems;
        private readonly Task delayItems;
        private readonly CancellationTokenSource tokenSource;
        private Action<T> handler;

        public ExpirableItemCollection(TimeSpan evaluationInterval)
        {
            delayedItems = new List<ExpirableItem>();
            tokenSource = new CancellationTokenSource();
            additionQueue = new BlockingCollection<ExpirableItem>(new ConcurrentQueue<ExpirableItem>());
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
            var delayedItem = new ExpirableItem(item, expireAfter);
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
            ExpirableItem item;
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

        private int Comparison(ExpirableItem expirableItem, ExpirableItem item)
            => expirableItem.ExpireAfter.CompareTo(item.ExpireAfter);
    }
}