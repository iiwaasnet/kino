using System.Collections.Generic;
using rawf.Connectivity;

namespace rawf.Client
{
    public interface IInitialRendezvousServerConfiguration
    {
        IEnumerable<RendezvousServerConfiguration> GetConfiguration();
    }
}