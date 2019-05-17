using kino.Core;

namespace kino.Cluster.Kafka
{
    public interface IKafkaScaleOutConfigurationProvider
    {
        KafkaNode GetScaleOutAddress();
    }
}