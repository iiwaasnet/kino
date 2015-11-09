using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C5;
using kino.Diagnostics;

namespace kino.Framework
{
    public class ExpirableItemCollection<T> : IExpirableItemCollection<T>
    {
        private const int ItemAdded = 0;
        private const int ProcessTerminated = 1;

        private readonly BlockingCollection<ExpirableItem<T>> additionQueue;
        private readonly Task checkExpirableItems;
        private readonly IntervalHeap<ExpirableItem<T>> expirableItems;
        private readonly ManualResetEventSlim itemAdded;
        private readonly CancellationTokenSource tokenSource;
        private Action<T> handler;
        private const int MaxAddCycles = 10000;
        private const int MaxDeleteCycles = 10000;
        private readonly ILogger logger;

        public ExpirableItemCollection(ILogger logger)
        {
            this.logger = logger;
            expirableItems = new IntervalHeap<ExpirableItem<T>>();
            tokenSource = new CancellationTokenSource();
            itemAdded = new ManualResetEventSlim(false);
            additionQueue = new BlockingCollection<ExpirableItem<T>>(new ConcurrentQueue<ExpirableItem<T>>());
            checkExpirableItems = Task.Factory.StartNew(_ => EvaluateDelays(tokenSource.Token), tokenSource.Token, TaskCreationOptions.LongRunning);
        }

        public void SetExpirationHandler(Action<T> handler)
        {
            this.handler = handler;
        }

        public void Dispose()
        {
            tokenSource.Cancel();
            additionQueue.CompleteAdding();
            checkExpirableItems.Wait();
            checkExpirableItems.Dispose();
            tokenSource.Dispose();
            additionQueue.Dispose();
        }

        public IExpirableItem Delay(T item, TimeSpan expireAfter)
        {
            var delayedItem = new ExpirableItem<T>(item, expireAfter);
            additionQueue.Add(delayedItem);
            itemAdded.Set();

            return delayedItem;
        }

        private void EvaluateDelays(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var smallestDelay = (expirableItems.Any())
                                                ? expirableItems.FindMin().ExpireAfter
                                                : Timeout.Infinite;
                        var reason = WaitHandle.WaitAny(new[] {itemAdded.WaitHandle, token.WaitHandle}, smallestDelay);
                        if (reason != ProcessTerminated)
                        {
                            if (reason == ItemAdded)
                            {
                                AddEnqueuedItems();
                            }
                            DeleteExpiredItems();
                        }
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void DeleteExpiredItems()
        {
            var now = DateTime.UtcNow;
            var iterations = MaxDeleteCycles;
            while (expirableItems.Any() && expirableItems.FindMin().IsExpired(now) && iterations-- > 0)
            {
                var item = expirableItems.DeleteMin().Item;
                if (handler != null)
                {
                    SafeNotifySubscriber(item);
                }
            }
        }

        private void AddEnqueuedItems()
        {
            ExpirableItem<T> item;
            var iterations = MaxAddCycles;
            while (additionQueue.TryTake(out item) && iterations-- > 0)
            {
                expirableItems.Add(item);
            }
            if (!additionQueue.Any())
            {
                itemAdded.Reset();
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
                logger.Error(err);
            }
        }
    }
}