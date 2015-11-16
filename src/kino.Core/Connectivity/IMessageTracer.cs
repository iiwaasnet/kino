using kino.Core.Messaging;

namespace kino.Core.Connectivity
{
    public interface IMessageTracer
    {
        void RoutedToLocalActor(Message message);
        void ForwardedToOtherNode(Message message);
        void ReceivedFromOtherNode(Message message);
    }
}