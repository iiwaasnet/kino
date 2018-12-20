using System.Collections.Generic;

namespace kino.Cluster.Configuration
{
    public interface IRendezvousClusterConfigurationProvider
    {
        RendezvousEndpoint GetCurrentRendezvousServer();

        bool SetCurrentRendezvousServer(RendezvousEndpoint newRendezvousServer);

        void RotateRendezvousServers();

        IEnumerable<RendezvousEndpoint> Nodes { get; }
    }
}