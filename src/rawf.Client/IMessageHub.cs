using System;
using rawf.Messaging;

namespace rawf.Client
{
    public interface IMessageHub
    {
        void SendOneWay(IMessage message);
        IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint);
        IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint, TimeSpan expireAfter);
        void Start();
        void Stop();
    }
}