using System;
using System.Collections.Generic;

namespace rawf.Connectivity
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        public ClusterConfiguration(ClusterMember thisNodeFrontEndSocket)
        {
        }

        public IEnumerable<ClusterMember> GetClusterMembers()
        {
            throw new NotImplementedException();
        }

        public void UpdateClusterMembers(IEnumerable<ClusterMember> newConfig)
        {
            throw new NotImplementedException();
        }
    }
}