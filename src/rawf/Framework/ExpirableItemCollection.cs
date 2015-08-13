﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using C5;

namespace rawf.Framework
{
    public class ExpirableItemCollection<T> : IExpirableItemCollection<T>
    {
        private readonly BlockingCollection<ExpirableItem<T>> additionQueue;
        private readonly IntervalHeap<ExpirableItem<T>> delayedItems;
        private readonly Task delayItems;
        private readonly CancellationTokenSource tokenSource;
        private readonly AutoResetEvent itemAdded;
        private Action<T> handler;
        private const int ItemAdded = 0;

        public ExpirableItemCollection()
        {
            delayedItems = new IntervalHeap<ExpirableItem<T>>(Comparer<ExpirableItem<T>>.Create(Comparison));
            tokenSource = new CancellationTokenSource();
            itemAdded = new AutoResetEvent(false);
            additionQueue = new BlockingCollection<ExpirableItem<T>>(new ConcurrentQueue<ExpirableItem<T>>());
            delayItems = Task.Factory.StartNew(_ => EvaluateDelays(tokenSource.Token), tokenSource.Token, TaskCreationOptions.LongRunning);
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
            itemAdded.Set();

            return delayedItem;
        }

        private void EvaluateDelays(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var smallestDelay = Timeout.InfiniteTimeSpan;
                    var reason = WaitHandle.WaitAny(new[] {itemAdded, token.WaitHandle}, smallestDelay);
                    if (reason == ItemAdded)
                    {
                        AddEnqueuedItems();
                    }
                    DeleteExpiredItems();
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

        private void DeleteExpiredItems()
        {
            var now = DateTime.UtcNow;
            while (delayedItems.Count > 0 && delayedItems.FindMin().IsExpired(now))
            {
                if (handler != null)
                {
                    SafeNotifySubscriber(delayedItems.DeleteMin().Item);
                }
            }
        }

        private void AddEnqueuedItems()
        {
            ExpirableItem<T> item;
            while (additionQueue.TryTake(out item))
            {
                delayedItems.Add(item);
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
        {
            var res = expirableItem.ExpireAfter.CompareTo(item.ExpireAfter);
            return res == 0 ? 1 : res;
        }
    }
}