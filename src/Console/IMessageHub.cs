using Console.Messages;

namespace Console
{
    public interface IMessageHub
    {
        IPromise EnqueueRequest(IMessage message, ICallbackPoint callbackPoint);
    }
}