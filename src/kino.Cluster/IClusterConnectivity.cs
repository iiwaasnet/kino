using System.Collections.Generic;
using kino.Core;

namespace kino.Cluster
{
    public interface IClusterConnectivity
    {
        void RegisterSelf(IEnumerable<Identifier> messageHandlers, string domain);

        void UnregisterSelf(IEnumerable<Identifier> messageIdentifiers);

        void DiscoverMessageRoute(Identifier messageIdentifier);

        void StartPeerMonitoring(SocketIdentifier socketIdentifier, Health health);

        void DeletePeer(SocketIdentifier socketIdentifier);

        void StartClusterServices();

        void StopClusterServices();
    }
}