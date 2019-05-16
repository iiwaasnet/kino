namespace kino.Connectivity.Kafka
{
    public interface IKafkaConnectionFactory
    {
        IListener CreateListener();

        IListener CreateListener(string groupId);

        ISender CreateSender();
    }
}