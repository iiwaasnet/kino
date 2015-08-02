using rawf.Messaging;

namespace rawf.Client
{
    public interface IMessageHub
    {
        IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint);
        void Start();
        void Stop();
    }
}