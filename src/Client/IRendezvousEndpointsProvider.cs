using System.Collections.Generic;
using rawf.Connectivity;

namespace Client
{
    public interface IRendezvousEndpointsProvider
    {
        IEnumerable<RendezvousEndpoints> GetConfiguration();
    }
}