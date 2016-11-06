using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public interface IClusterConnectivity
    {
        void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain);

        void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers);

        void DiscoverMessageRoute(Identifier messageIdentifier);

        void StartPeerMonitoring(Node peer, Health health);

        void AddPeer(Node peer, Health health);

        void DeletePeer(SocketIdentifier socketIdentifier);

        void StartClusterServices();

        void StopClusterServices();
    }
}