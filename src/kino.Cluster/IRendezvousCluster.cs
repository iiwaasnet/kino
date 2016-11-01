using System.Collections.Generic;
using kino.Configuration;

namespace kino.Cluster
{
    public interface IRendezvousCluster
    {
        RendezvousEndpoint GetCurrentRendezvousServer();
        void SetCurrentRendezvousServer(RendezvousEndpoint currentRendezvousServer);
        void RotateRendezvousServers();
        void Reconfigure(IEnumerable<RendezvousEndpoint> newConfiguration);
    }
}