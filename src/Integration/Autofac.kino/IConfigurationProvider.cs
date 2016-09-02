using System.Collections.Generic;
using kino.Client;
using kino.Core.Connectivity;

namespace Autofac.kino
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();

        RouterConfiguration GetRouterConfiguration();

        ScaleOutSocketConfiguration GetScaleOutConfiguration();

        ClusterMembershipConfiguration GetClusterMembershipConfiguration();

        MessageHubConfiguration GetMessageHubConfiguration();

        ClusterMembershipConfiguration GetClusterTimingConfiguration();
    }
}