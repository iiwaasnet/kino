using Console.Messages;

namespace Console
{
    public interface IClientRequestSink
    {
        IPromise EnqueueRequest(IMessage message);
    }
}