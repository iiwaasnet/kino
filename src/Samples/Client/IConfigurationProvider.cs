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
        ClusterMembershipConfiguration GetClusterMembershipConfiguration();
        ExpirableItemCollectionConfiguration GetExpirableItemCollectionConfiguration();
        MessageHubConfiguration GetMessageHubConfiguration();
    }
}