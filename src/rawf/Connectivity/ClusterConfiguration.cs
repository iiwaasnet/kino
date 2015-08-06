using System.Collections.Generic;

namespace rawf.Connectivity
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        private readonly HashSet<SocketEndpoint> clusterMembers;

        public ClusterConfiguration()
        {
            clusterMembers = new HashSet<SocketEndpoint>();
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembers;

        public void AddClusterMember(SocketEndpoint node)
            => clusterMembers.Add(node);
    }
}