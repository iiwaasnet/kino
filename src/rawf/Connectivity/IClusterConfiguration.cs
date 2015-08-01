using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterConfiguration
    {
        IEnumerable<ClusterMember> GetClusterMembers();
        void AddClusterMember(ClusterMember node);
    }
}