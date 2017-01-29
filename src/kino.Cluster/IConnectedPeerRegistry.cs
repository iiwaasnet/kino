using kino.Core;

namespace kino.Cluster
{
    public interface IConnectedPeerRegistry
    {
        ClusterMemberMeta Find(ReceiverIdentifier peer);

        ClusterMemberMeta FindOrAdd(ReceiverIdentifier peer, ClusterMemberMeta meta);

        void Remove(ReceiverIdentifier peer);

        System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<ReceiverIdentifier, ClusterMemberMeta>> GetPeersWithExpiredHeartBeat();

        int Count();
    }
}