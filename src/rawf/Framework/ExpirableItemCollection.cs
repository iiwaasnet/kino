using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using C5;

namespace rawf.Framework
{
  public class ExpirableItemCollection<T> : IExpirableItemCollection<T>
  {
    private const int ItemAdded = 0;
    private const int ProcessTerminated = 1;

    private readonly BlockingCollection<ExpirableItem<T>> additionQueue;
    private readonly Task checkExpirableItems;
    private readonly IntervalHeap<ExpirableItem<T>> expirableItems;
    private readonly AutoResetEvent itemAdded;
    private readonly CancellationTokenSource tokenSource;
    private Action<T> handler;

    public ExpirableItemCollection()
    {
      expirableItems = new IntervalHeap<ExpirableItem<T>>(Comparer<ExpirableItem<T>>.Create(Comparison));
      tokenSource = new CancellationTokenSource();
      itemAdded = new AutoResetEvent(false);
      additionQueue = new BlockingCollection<ExpirableItem<T>>(new ConcurrentQueue<ExpirableItem<T>>());
      checkExpirableItems = Task.Factory.StartNew(_ => EvaluateDelays(tokenSource.Token), tokenSource.Token, TaskCreationOptions.LongRunning);
    }

    public void SetExpirationHandler(Action<T> handler)
    {
      this.handler = handler;
    }

    public void Dispose()
    {
      tokenSource.Cancel(true);
      additionQueue.CompleteAdding();
      checkExpirableItems.Wait();
      checkExpirableItems.Dispose();
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
          var smallestDelay = (expirableItems.Any())
            ? expirableItems.FindMin().ExpireAfter
            : Timeout.Infinite;
          var reason = WaitHandle.WaitAny(new[] {itemAdded, token.WaitHandle}, smallestDelay);
          if (reason != ProcessTerminated)
          {
            if (reason == ItemAdded)
            {
              AddEnqueuedItems();
            }
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

    private void DeleteExpiredItems()
    {
      var now = DateTime.UtcNow;
      while (expirableItems.Any() && expirableItems.FindMin().IsExpired(now))
      {
        if (handler != null)
        {
          SafeNotifySubscriber(expirableItems.DeleteMin().Item);
        }
      }
      Console.WriteLine($"Promise left:{expirableItems.Count}");
    }

    private void AddEnqueuedItems()
    {
      ExpirableItem<T> item;
      while (additionQueue.TryTake(out item))
      {
        expirableItems.Add(item);
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