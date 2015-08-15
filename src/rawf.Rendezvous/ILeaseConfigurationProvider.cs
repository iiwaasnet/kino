using rawf.Consensus;

namespace rawf.Rendezvous
{
    public interface ILeaseConfigurationProvider
    {
        ILeaseConfiguration GetConfiguration();
    }
}