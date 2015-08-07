using System;
using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterConfiguration
    {
        IEnumerable<SocketEndpoint> GetClusterMembers();
        IEnumerable<SocketEndpoint> GetDeadMembers();
        void AddClusterMember(SocketEndpoint node);
        void DeleteClusterMember(SocketEndpoint node);
        bool KeepAlive(SocketEndpoint node);
        TimeSpan PingSilenceBeforeRendezvousFailover { get; }
        TimeSpan PongSilenceBeforeRouteDeletion { get; } 
    }
}