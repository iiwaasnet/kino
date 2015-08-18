using System.Collections.Generic;
using rawf.Connectivity;

namespace Server
{
    public interface IRendezvousEndpointsProvider
    {
        IEnumerable<RendezvousEndpoints> GetConfiguration();
    }
}