using System.Collections.Generic;
using kino.Configuration;

namespace Autofac.kino
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();

        RouterConfiguration GetRouterConfiguration();

        ScaleOutSocketConfiguration GetScaleOutConfiguration();

        ClusterMembershipConfiguration GetClusterMembershipConfiguration();

        ClusterMembershipConfiguration GetClusterTimingConfiguration();
    }
}