using System;
using System.Collections.Generic;

namespace kino.Connectivity
{
    public interface IClusterConfiguration
    {
        IEnumerable<SocketEndpoint> GetClusterMembers();
        IEnumerable<SocketEndpoint> GetDeadMembers(DateTime pingTime);
        void AddClusterMember(SocketEndpoint node);
        void DeleteClusterMember(SocketEndpoint node);
        bool KeepAlive(SocketEndpoint node);        
    }
}