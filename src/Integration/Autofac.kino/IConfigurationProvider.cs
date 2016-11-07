using System.Collections.Generic;
using kino.Cluster.Configuration;

namespace Autofac.kino
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();

        RouterConfiguration GetRouterConfiguration();

        ScaleOutSocketConfiguration GetScaleOutConfiguration();

        ClusterMembershipConfiguration GetClusterMembershipConfiguration();

        ClusterHealthMonitorConfiguration GetClusterHealthMonitorConfiguration();

        HeartBeatSenderConfiguration GetHeartBeatSenderConfiguration();
    }
}