using System.Collections.Generic;

namespace rawf.Connectivity
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        private readonly HashSet<ClusterMember> clusterMembers;

        public ClusterConfiguration()
        {
            clusterMembers = new HashSet<ClusterMember>();
        }

        public IEnumerable<ClusterMember> GetClusterMembers()
        {
            return clusterMembers;
        }

        public void AddClusterMember(ClusterMember node)
        {
            clusterMembers.Add(node);
        }
    }
}