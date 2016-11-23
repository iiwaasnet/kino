using System.Collections.Generic;
using kino.Cluster.Configuration;

namespace kino.Configuration
{
    public interface IConfigurationProvider
    {
        IEnumerable<RendezvousEndpoint> GetRendezvousEndpointsConfiguration();

        ScaleOutSocketConfiguration GetScaleOutConfiguration();

        ClusterMembershipConfiguration GetClusterMembershipConfiguration();

        ClusterHealthMonitorConfiguration GetClusterHealthMonitorConfiguration();

        HeartBeatSenderConfiguration GetHeartBeatSenderConfiguration();
    }
}