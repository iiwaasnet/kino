using System;

namespace Console.Messages
{
    public interface IMessage
    {
        T GetPayload<T>() where T : IPayload;

        DistributionPattern Distribution { get; }

        string Version { get; }
        string Identity { get; }
        byte[] CorrelationId { get; }
        byte[] ReceiverId { get; }

        byte EndOfFlowIdentity { get; }
        byte[] EndOfFlowReceiverId { get; }

        TimeSpan TTL { get; set; }
        byte[] Body { get; }
    }

    public enum DistributionPattern
    {
        Unicast = 0,
        Broadcast = 1
    }
}