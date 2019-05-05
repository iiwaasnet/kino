namespace kino.Cluster.Kafka
{
    public interface IKafkaClusterServices
    {
        IClusterMonitor GetClusterMonitor();

        IKafkaClusterHealthMonitor GetClusterHealthMonitor();

        void StopClusterServices();

        void StartClusterServices();
    }
}