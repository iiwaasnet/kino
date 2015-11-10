using System.Collections.Generic;
using kino.Client;
using kino.Connectivity;

namespace Client
{
    public interface IConfigurationProvider
    {
        IEnumerable<kino.Connectivity.RendezvousEndpoint> GetRendezvousEndpointsConfiguration();
        RouterConfiguration GetRouterConfiguration();
        ClusterMembershipConfiguration GetClusterMembershipConfiguration();
        MessageHubConfiguration GetMessageHubConfiguration();
    }
}