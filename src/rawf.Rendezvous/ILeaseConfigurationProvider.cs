using rawf.Rendezvous.Consensus;

namespace rawf.Rendezvous
{
    public interface ILeaseConfigurationProvider
    {
        ILeaseConfiguration GetConfiguration();
    }
}