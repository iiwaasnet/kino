using kino.Messaging;

namespace kino.Connectivity
{
    public interface IMessageTracer
    {
        void RoutedToLocalActor(Message message);
        void ForwardedToOtherNode(Message message);
        void ReceivedFromOtherNode(Message message);
    }
}