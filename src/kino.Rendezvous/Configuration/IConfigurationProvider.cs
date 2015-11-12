using kino.Consensus;
using kino.Consensus.Configuration;

namespace kino.Rendezvous.Configuration
{
    public interface IConfigurationProvider
    {
        LeaseConfiguration GetLeaseConfiguration();
        RendezvousConfiguration GetRendezvousConfiguration();
    }
}