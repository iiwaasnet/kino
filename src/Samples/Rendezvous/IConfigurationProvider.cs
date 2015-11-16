using kino.Consensus.Configuration;
using kino.Rendezvous.Configuration;

namespace Rendezvous
{
    public interface IConfigurationProvider
    {
        LeaseConfiguration GetLeaseConfiguration();
        RendezvousConfiguration GetRendezvousConfiguration();
    }
}