using System;
using System.Collections.Generic;

namespace kino.Core.Connectivity
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