using System;

namespace rawf.Messaging
{
    public interface IMessage
    {
        T GetPayload<T>() where T : IPayload;

        DistributionPattern Distribution { get; }

        byte[] Version { get; }
        byte[] Identity { get; }
        byte[] CorrelationId { get; }
        byte[] ReceiverIdentity { get; }

        byte[] CallbackIdentity { get; }
        byte[] CallbackReceiverIdentity { get; }

        TimeSpan TTL { get; set; }
        byte[] Body { get; }
        
    }

    public enum DistributionPattern
    {
        Unicast = 0,
        Broadcast = 1
    }
}