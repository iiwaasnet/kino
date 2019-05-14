using kino.Core;

namespace kino.Cluster.Kafka.Configuration
{
    public interface INodeIdentityProvider
    {
        KafkaNode GetNodeIdentity();
    }
}