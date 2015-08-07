using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace rawf.Connectivity
{
    public class ClusterConfiguration : IClusterConfiguration
    {
        private readonly ConcurrentDictionary<SocketEndpoint, ClusterMemberMeta> clusterMembers;

        public ClusterConfiguration()
        {
            clusterMembers = new ConcurrentDictionary<SocketEndpoint, ClusterMemberMeta>();
        }

        public IEnumerable<SocketEndpoint> GetClusterMembers()
            => clusterMembers.Keys;

        public void AddClusterMember(SocketEndpoint node)
            => clusterMembers.TryAdd(node, new ClusterMemberMeta { LastKnownPong = DateTime.UtcNow });
            
        public bool KeepAlive(SocketEndpoint node)
        {
            ClusterMemberMeta meta;
            var updated = clusterMembers.TryGetValue(node, out meta);
            if (updated)
            {
                meta.LastKnownPong = DateTime.UtcNow;
            }
            
            return updated;
        }

        public TimeSpan PingSilenceBeforeRendezvousFailover { get; set; }
        public TimeSpan PongSilenceBeforeRouteDeletion { get; set; }
    }
}