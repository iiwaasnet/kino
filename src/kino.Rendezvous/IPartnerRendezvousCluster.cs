using System.Collections.Generic;
using kino.Rendezvous.Configuration;

namespace kino.Rendezvous
{
    public interface IPartnerRendezvousCluster
    {
        PartnerRendezvousEndpoint GetCurrentRendezvousServer();

        bool SetCurrentRendezvousServer(PartnerRendezvousEndpoint newRendezvousServer);

        void RotateRendezvousServers();

        void Reconfigure(IEnumerable<PartnerRendezvousEndpoint> newConfiguration);

        IEnumerable<PartnerRendezvousEndpoint> Nodes { get; }
    }
}