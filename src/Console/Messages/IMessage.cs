namespace Console.Messages
{
    public interface IMessage
    {
        byte[] Body { get; }
        string Identity { get; }
        string Version { get; }
        long TTL { get; }
        DistributionPattern Distribution { get; }
    }

    public enum DistributionPattern
    {
        Unicast = 0,
        Broadcast = 1
    }
}