using rawf.Messaging;

namespace rawf.Frontend
{
    public interface IMessageHub
    {
        IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint);
    }
}