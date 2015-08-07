using System;
using System.Collections.Generic;

namespace rawf.Connectivity
{
    public interface IClusterConfiguration
    {
        IEnumerable<SocketEndpoint> GetClusterMembers();
        void AddClusterMember(SocketEndpoint node);
        bool KeepAlive(SocketEndpoint node);
        TimeSpan PingSilenceBeforeRendezvousFailover { get; }
        TimeSpan PongSilenceBeforeRouteDeletion { get; } 
    }
}