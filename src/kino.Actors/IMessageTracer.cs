using kino.Messaging;

namespace kino.Actors
{
    public interface IMessageTracer
    {
        void HandlerNotFound(IMessage message);
        void ResponseSent(IMessage message, bool sentSync);
        void MessageProcessed(IMessage message, int responseCount);
    }
}