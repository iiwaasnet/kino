using kino.Rendezvous.Consensus;

namespace kino.Rendezvous.Configuration
{
    public interface IConfigurationProvider
    {
        LeaseConfiguration GetLeaseConfiguration();
        RendezvousConfiguration GetRendezvousConfiguration();
    }
}