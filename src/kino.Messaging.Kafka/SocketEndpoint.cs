namespace kino.Messaging.Kafka
{
    public class SocketEndpoint
    {
        public string BrokerUri { get; set; }

        public byte[] Identity { get; set; }
    }
}