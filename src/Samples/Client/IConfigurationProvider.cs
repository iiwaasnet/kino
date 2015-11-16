using System.Collections.Generic;
using kino.Client;
using kino.Core.Connectivity;

namespace Client
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();
        RouterConfiguration GetRouterConfiguration();
        ClusterMembershipConfiguration GetClusterMembershipConfiguration();
        MessageHubConfiguration GetMessageHubConfiguration();
    }
}