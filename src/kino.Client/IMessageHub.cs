using kino.Core.Messaging;

namespace kino.Client
{
    public interface IMessageHub
    {
        void SendOneWay(IMessage message);

        IPromise EnqueueRequest(IMessage message, CallbackPoint callbackPoint);

        void Start();

        void Stop();
    }
}