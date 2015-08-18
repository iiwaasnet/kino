using rawf.Rendezvous.Consensus;

namespace rawf.Rendezvous
{
    public interface ILeaseConfigurationProvider
    {
        LeaseConfiguration GetConfiguration();
    }
}