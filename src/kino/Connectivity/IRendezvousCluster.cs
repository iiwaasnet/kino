using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IRendezvousCluster
    {
        RendezvousEndpoint GetCurrentRendezvousServer();
        void SetCurrentRendezvousServer(RendezvousEndpoint currentRendezvousServer);
        void RotateRendezvousServers();
        void Reconfigure(IEnumerable<RendezvousEndpoint> newConfiguration);
    }
}