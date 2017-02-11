using kino.Core;
using BclKeyValuePair = System.Collections.Generic.KeyValuePair<kino.Core.ReceiverIdentifier, kino.Cluster.ClusterMemberMeta>;
namespace kino.Cluster
{
    public interface IConnectedPeerRegistry
    {
        ClusterMemberMeta Find(ReceiverIdentifier peer);

        ClusterMemberMeta FindOrAdd(ReceiverIdentifier peer, ClusterMemberMeta meta);

        void Remove(ReceiverIdentifier peer);

        System.Collections.Generic.IEnumerable<BclKeyValuePair> GetPeersWithExpiredHeartBeat();

        System.Collections.Generic.IEnumerable<BclKeyValuePair> GetStalePeers();

        int Count();
    }
}