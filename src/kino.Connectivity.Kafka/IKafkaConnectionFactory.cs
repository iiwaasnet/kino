using kino.Core;

namespace kino.Connectivity.Kafka
{
    public interface IKafkaConnectionFactory
    {
        IListener GetListener(string uri, string groupId, string topic);

        ISender GetSender(KafkaNode node);

        ISender CreateSender(KafkaSenderConfiguration config);

        IListener CreateListener(KafkaListenerConfiguration config);
    }
}