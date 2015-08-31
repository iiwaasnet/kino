using kino.Rendezvous.Consensus;

namespace kino.Rendezvous
{
    public interface ILeaseConfigurationProvider
    {
        LeaseConfiguration GetConfiguration();
    }
}