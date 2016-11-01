using System;
using System.Collections.Generic;
using kino.Core.Connectivity;

namespace kino.Cluster
{
    public interface IClusterMembership
    {
        IEnumerable<SocketEndpoint> GetClusterMembers();

        IEnumerable<SocketEndpoint> GetDeadMembers(DateTime pingTime, TimeSpan pingInterval);

        void AddClusterMember(SocketEndpoint node);

        void DeleteClusterMember(SocketEndpoint node);

        bool KeepAlive(SocketEndpoint node);
    }
}