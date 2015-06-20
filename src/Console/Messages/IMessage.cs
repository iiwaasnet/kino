using System;

namespace Console.Messages
{
    public interface IMessage
    {
        T GetPayload<T>() where T : IPayload;

        byte[] Body { get; }
        string Identity { get; }
        string Version { get; }
        TimeSpan TTL { get; set; }
        DistributionPattern Distribution { get; }
    }

    public enum DistributionPattern
    {
        Unicast = 0,
        Broadcast = 1
    }
}