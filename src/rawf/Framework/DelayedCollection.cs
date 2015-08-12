using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace rawf.Framework
{
    public partial class DelayedCollection<T> : IDelayedCollection<T>
    {
        private readonly BlockingCollection<DelayedItem> additionQueue;
        private readonly List<DelayedItem> delayedItems;
        private readonly Task delayItems;
        private readonly CancellationTokenSource tokenSource;
        private readonly AutoResetEvent itemAdded;
        private Action<T> handler;

        public DelayedCollection()
        {
            delayedItems = new List<DelayedItem>();
            itemAdded = new AutoResetEvent(false);
            tokenSource = new CancellationTokenSource();
            additionQueue = new BlockingCollection<DelayedItem>(new ConcurrentQueue<DelayedItem>());
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

        private void EvaluateDelays(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    DelayedItem item;
                    while (additionQueue.TryTake(out item))
                    {
                        delayedItems.Add(item);
                    }
                    var now = DateTime.UtcNow;

                    delayedItems.Sort(Comparison);
                    while (delayedItems.Count > 0 && delayedItems[0].IsExpired(now))
                    {
                        if (handler != null)
                        {
                            SafeNotifySubscriber(delayedItems[0].Item);
                        }
                        delayedItems.RemoveAt(0);
                    }
                    var sleep = (delayedItems.Count > 0) ? delayedItems[0].ExpireAfter : Timeout.Infinite;

                    WaitHandle.WaitAny(new[] {itemAdded, token.WaitHandle}, sleep);
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

        private int Comparison(DelayedItem delayedItem, DelayedItem item)
            => delayedItem.ExpireAfter.CompareTo(item.ExpireAfter);

        public IDelayedItem Delay(T item, TimeSpan expireAfter)
        {
            var delayedItem = new DelayedItem(item, expireAfter, this);
            additionQueue.Add(delayedItem);
            itemAdded.Set();

            return delayedItem;
        }

        private void TriggerDelayEvaluation()
            => itemAdded.Set();
    }
}