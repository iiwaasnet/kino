using rawf.Messaging;

namespace rawf.Client
{
    public interface IMessageTracer
    {
        void CallbackRegistered(IMessage message);
        void SentToRouter(IMessage message);
        void CallbackResultSet(IMessage message);
        void CallbackNotFound(IMessage message);
    }
}