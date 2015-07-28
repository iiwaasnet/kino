using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterConfigurationMonitor
    {
        void Start();
        void Stop();
        void RegisterMember(ClusterMember member, IEnumerable<MessageHandlerIdentifier> messageHandlers);
        void UnregisterMember(ClusterMember member);
    }
}