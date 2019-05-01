using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public interface ISender
    {
        void Send(string destination, IMessage message);
    }
}