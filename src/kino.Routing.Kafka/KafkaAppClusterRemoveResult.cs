namespace kino.Routing.Kafka
{
    public class KafkaAppClusterRemoveResult
    {
        public KafkaAppCluster AppCluster { get; set; }

        public BrokerConnectionAction ConnectionAction { get; set; }
    }
}