using System.Collections.Generic;
using kino.Connectivity;

namespace Client
{
    public interface IRendezvousEndpointsProvider
    {
        IEnumerable<RendezvousEndpoints> GetConfiguration();
    }
}