using kino.Rendezvous.Consensus;

namespace kino.Rendezvous.Configuration
{
    public interface ILeaseConfigurationProvider
    {
        LeaseConfiguration GetConfiguration();
    }
}