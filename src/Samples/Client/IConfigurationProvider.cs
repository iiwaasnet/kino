using System.Collections.Generic;
using kino.Client;
using kino.Connectivity;
using kino.Framework;

namespace Client
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoints> GetRendezvousEndpointsConfiguration();
        RouterConfiguration GetRouterConfiguration();
        ClusterTimingConfiguration GetClusterTimingConfiguration();
        ExpirableItemCollectionConfiguration GetExpirableItemCollectionConfiguration();
        MessageHubConfiguration GetMessageHubConfiguration();
    }
}