using System;
using System.Linq;
using C5;
using kino.Cluster.Configuration;
using kino.Core;

namespace kino.Cluster
{
    public class ConnectedPeerRegistry : IConnectedPeerRegistry
    {
        private readonly ClusterHealthMonitorConfiguration config;
        private readonly HashDictionary<ReceiverIdentifier, ClusterMemberMeta> peers;

        public ConnectedPeerRegistry(ClusterHealthMonitorConfiguration config)
        {
            peers = new HashDictionary<ReceiverIdentifier, ClusterMemberMeta>();
            this.config = config;
        }

        public ClusterMemberMeta Find(ReceiverIdentifier peer)
        {
            ClusterMemberMeta meta;
            return peers.Find(ref peer, out meta) ? meta : null;
        }

        public ClusterMemberMeta FindOrAdd(ReceiverIdentifier peer, ClusterMemberMeta meta)
        {
            peers.FindOrAdd(peer, ref meta);
            return meta;
        }

        public void Remove(ReceiverIdentifier peer)
            => peers.Remove(peer);

        public System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>> GetPeersWithExpiredHeartBeat()
        {
            var now = DateTime.UtcNow;
            return peers.Where(p => PeerIsStale(now, p))
                        .Select(p => new System.Collections.Generic.KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>(p.Key, p.Value))
                        .ToList();
        }

        public int Count()
            => peers.Count();

        private bool PeerIsStale(DateTime now, KeyValuePair<ReceiverIdentifier, ClusterMemberMeta> peer)
            => !peer.Value.ConnectionEstablished
               && now - peer.Value.LastKnownHeartBeat > config.PeerIsStaleAfter;
    }
}