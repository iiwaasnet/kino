using kino.Core;

namespace kino.Routing.Kafka.ServiceMessageHandlers
{
    public interface IKafkaServiceMessageHandlerRegistry
    {
        IKafkaServiceMessageHandler GetMessageHandler(MessageIdentifier message);
    }
}