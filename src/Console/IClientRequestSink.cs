using Console.Messages;

namespace Console
{
    public interface IClientRequestSink
    {
        IPromise<T> EnqueueRequest<T>(IMessage message) where T : IMessage;
    }
}