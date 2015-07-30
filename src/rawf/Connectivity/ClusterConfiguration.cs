using System;
using System.Collections.Generic;
using System.Linq;

namespace rawf.Connectivity
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        private IEnumerable<ClusterMember> clusterMembers;

        public ClusterConfiguration(ClusterMember thisNodeFrontEndSocket)
        {
            clusterMembers = new[] {thisNodeFrontEndSocket};
        }

        public IEnumerable<ClusterMember> GetClusterMembers()
        {
            return clusterMembers;
        }

        public void TryAddClusterMember(ClusterMember node)
        {
            throw new NotImplementedException();
        }
    }
}