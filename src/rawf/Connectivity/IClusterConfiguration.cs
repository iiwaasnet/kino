using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterConfiguration
    {
        IEnumerable<SocketEndpoint> GetClusterMembers();
        void AddClusterMember(SocketEndpoint node);
    }
}