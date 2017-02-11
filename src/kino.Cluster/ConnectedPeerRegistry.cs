using System;
using System.Collections.Generic;
using System.Linq;
using C5;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using BclKeyValuePair = System.Collections.Generic.KeyValuePair<kino.Core.ReceiverIdentifier, kino.Cluster.ClusterMemberMeta>;

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

        public IEnumerable<BclKeyValuePair> GetPeersWithExpiredHeartBeat()
        {
            var now = DateTime.UtcNow;
            return peers.Where(p => HeartBeatExpired(now, p))
                        .Select(p => new BclKeyValuePair(p.Key, p.Value))
                        .ToList();
        }

        public IEnumerable<BclKeyValuePair> GetStalePeers()
        {
            var now = DateTime.UtcNow;
            return peers.Where(p => PeerIsStale(now, p))
                        .Select(p => new BclKeyValuePair(p.Key, p.Value))
                        .ToList();
        }

        public int Count()
            => peers.Count();

        private bool HeartBeatExpired(DateTime now, C5.KeyValuePair<ReceiverIdentifier, ClusterMemberMeta> peer)
            => peer.Value.ConnectionEstablished
               && now - peer.Value.LastKnownHeartBeat > peer.Value.HeartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion);

        private bool PeerIsStale(DateTime now, C5.KeyValuePair<ReceiverIdentifier, ClusterMemberMeta> peer)
            => !peer.Value.ConnectionEstablished
               && now - peer.Value.LastKnownHeartBeat > config.PeerIsStaleAfter;
    }
}