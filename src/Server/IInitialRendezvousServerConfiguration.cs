using System.Collections.Generic;
using rawf.Connectivity;

namespace Server
{
    public interface IInitialRendezvousServerConfiguration
    {
        IEnumerable<RendezvousServerConfiguration> GetConfiguration();
    }
}