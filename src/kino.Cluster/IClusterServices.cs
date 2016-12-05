using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public interface IClusterServices
    {
        void DiscoverMessageRoute(MessageRoute messageRoute);

        void RegisterSelf(IEnumerable<MessageRoute> registrations, string domain);

        void UnregisterSelf(IEnumerable<MessageRoute> registrations);

        void StartPeerMonitoring(Node node, Health health);

        void AddPeer(Node peer, Health health);

        void DeletePeer(ReceiverIdentifier nodeIdentifier);

        void StopClusterServices();

        void StartClusterServices();
    }
}