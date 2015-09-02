using System.Collections.Generic;
using kino.Connectivity;

namespace Server
{
    public interface IRendezvousEndpointsProvider
    {
        IEnumerable<RendezvousEndpoints> GetConfiguration();
    }
}