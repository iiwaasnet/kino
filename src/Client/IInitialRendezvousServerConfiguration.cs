using System.Collections.Generic;
using rawf.Connectivity;

namespace Client
{
    public interface IInitialRendezvousServerConfiguration
    {
        IEnumerable<RendezvousServerConfiguration> GetConfiguration();
    }
}