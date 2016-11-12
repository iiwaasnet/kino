using System;
using System.Collections.Concurrent;
using System.Threading;
using kino.Core;

namespace kino.Connectivity
{
    public class LocalSocket<T> : ILocalSocket<T>, IEquatable<LocalSocket<T>>
    {
        private readonly ManualResetEvent dataAvailable;
        private readonly BlockingCollection<T> messageQueue;
        private readonly BlockingCollection<T> lookAheadQueue;
        private readonly SocketIdentifier socketIdentity;
        private readonly int hashCode;

        public LocalSocket()
        {
            dataAvailable = new ManualResetEvent(false);
            socketIdentity = SocketIdentifier.Create();
            messageQueue = new BlockingCollection<T>(new ConcurrentQueue<T>());
            lookAheadQueue = new BlockingCollection<T>(new ConcurrentQueue<T>());
            hashCode = socketIdentity.GetHashCode();
        }

        public void Send(T message)
        {
            messageQueue.Add(message);
            dataAvailable.Set();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((LocalSocket<T>) obj);
        }

        public bool Equals(LocalSocket<T> other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return GetIdentity() == other.GetIdentity();
        }

        public override int GetHashCode()
            => hashCode;

        public T TryReceive()
        {
            T lookup,
              message;
            if (!lookAheadQueue.TryTake(out message))
            {
                messageQueue.TryTake(out message);
            }
            if (!messageQueue.TryTake(out lookup))
            {
                dataAvailable.Reset();
            }
            else
            {
                lookAheadQueue.Add(lookup);
            }

            return message;
        }

        public WaitHandle CanReceive()
            => dataAvailable;

        public SocketIdentifier GetIdentity()
            => socketIdentity;
    }
}