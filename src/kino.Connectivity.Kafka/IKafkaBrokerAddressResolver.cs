namespace kino.Connectivity.Kafka
{
    public interface IKafkaBrokerAddressResolver
    {
        string GetBootstrapServers(string brokerName);
    }
}