using System;
using System.Collections.Concurrent;
using rawf.Diagnostics;
using rawf.Messaging;

namespace rawf.Consensus
{
    public class Listener : IListener
    {
        private readonly ConcurrentDictionary<IObserver<IMessage>, object> observers;
        //private readonly BlockingCollection<IMessage> messages;
        //private Action<IMessage> appendMessage;
        private readonly Action<Listener> unsubscribe;
        private readonly ILogger logger;

        public Listener(Action<Listener> unsubscribe, ILogger logger)
        {
            observers = new ConcurrentDictionary<IObserver<IMessage>, object>();
            //messages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            //appendMessage = DropMessage;
            this.unsubscribe = unsubscribe;
            this.logger = logger;
            ////TODO: might be quite expensive to spawn every time new thread
            //new Thread(ForwardMessages).Start();
        }

        public void Notify(IMessage message)
        {
            foreach (var observer in observers.Keys)
            {
                try
                {
                    observer.OnNext(message);
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }
        }

        //private void AddMessageToQueue(IMessage message)
        //{
        //    messages.Add(message);
        //}

        //private void DropMessage(IMessage message)
        //{
        //}

        //private void ForwardMessages()
        //{
        //    foreach (var message in messages.GetConsumingEnumerable())
        //    {
        //        foreach (var observer in observers.Keys)
        //        {
        //            try
        //            {
        //                observer.OnNext(message);
        //            }
        //            catch (Exception err)
        //            {
        //                logger.Error(err);
        //            }
        //        }
        //    }
        //    messages.Dispose();
        //}

        public IDisposable Subscribe(IObserver<IMessage> observer)
        {
            observers[observer] = null;

            return new Unsubscriber(observers, observer);
        }

        //public void Start()
        //{
        //    Interlocked.Exchange(ref appendMessage, AddMessageToQueue);
        //}

        //public void Stop()
        //{
        //    Interlocked.Exchange(ref appendMessage, DropMessage);
        //}

        public void Dispose()
        {
            unsubscribe(this);
            //Stop();
            //messages.CompleteAdding();
        }

        private class Unsubscriber : IDisposable
        {
            private readonly ConcurrentDictionary<IObserver<IMessage>, object> observers;
            private readonly IObserver<IMessage> observer;

            public Unsubscriber(ConcurrentDictionary<IObserver<IMessage>, object> observers, IObserver<IMessage> observer)
            {
                this.observer = observer;
                this.observers = observers;
            }

            public void Dispose()
            {
                if (observer != null)
                {
                    object val;
                    observers.TryRemove(observer, out val);
                }
            }
        }
    }
}