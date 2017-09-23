using System;
using System.Linq;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Core;
using kino.Core.Framework;
using kino.Tests.Helpers;
using NUnit.Framework;
using KVP = System.Collections.Generic.KeyValuePair<kino.Core.ReceiverIdentifier, kino.Cluster.ClusterMemberMeta>;

namespace kino.Tests.Cluster
{
    
    public class ConnectedPeerRegistryTests
    {
        private ConnectedPeerRegistry peerRegistry;
        private ClusterHealthMonitorConfiguration config;

        
        public void Setup()
        {
            config = new ClusterHealthMonitorConfiguration
                     {
                         PeerIsStaleAfter = TimeSpan.FromSeconds(2),
                         MissingHeartBeatsBeforeDeletion = 3
                     };
            peerRegistry = new ConnectedPeerRegistry(config);
        }

        [Fact]
        public void IfReceiverIdentifierDoesntExist_FundReturnsNull()
        {
            var onePeer = ReceiverIdentities.CreateForActor();
            var anotherPeer = ReceiverIdentities.CreateForActor();
            peerRegistry.FindOrAdd(onePeer, new ClusterMemberMeta());
            //
            Assert.Null(peerRegistry.Find(anotherPeer));
        }

        [Fact]
        public void FindOrAdd_AddsReceiverIdentityIfItDoesntExistsAndReturnsClusterMemberData()
        {
            var peer = ReceiverIdentities.CreateForActor();
            var clusterMemberMeta = new ClusterMemberMeta();
            //
            Assert.Equal(clusterMemberMeta, peerRegistry.FindOrAdd(peer, clusterMemberMeta));
        }

        [Fact]
        public void DuplicatedReceiverIdentities_CanNotBeAdded()
        {
            var peer = ReceiverIdentities.CreateForActor();
            var clusterMemberMeta = new ClusterMemberMeta();
            //
            peerRegistry.FindOrAdd(peer, clusterMemberMeta);
            peerRegistry.FindOrAdd(peer, clusterMemberMeta);
            //
            Assert.Equal(1, peerRegistry.Count());
        }

        [Fact]
        public void Remove_DeletesOnlyCorrespondingReceiverIdentifier()
        {
            var onePeer = ReceiverIdentities.CreateForActor();
            var anotherPeer = ReceiverIdentities.CreateForActor();
            peerRegistry.FindOrAdd(onePeer, new ClusterMemberMeta());
            peerRegistry.FindOrAdd(anotherPeer, new ClusterMemberMeta());
            Assert.Equal(2, peerRegistry.Count());
            //
            peerRegistry.Remove(anotherPeer);
            //
            Assert.Null(peerRegistry.Find(anotherPeer));
            Assert.NotNull(peerRegistry.Find(onePeer));
            Assert.Equal(1, peerRegistry.Count());
        }

        [Fact]
        public void GetPeersWithExpiredHeartBeat_ReturnsPeersWichAreConnectedAndLastKnownHeartBeatFromNowGreaterThanPeerHeartBeatIntervalTimesMissingHeartBeatsBeforeDeletion()
        {
            var heartBeatInterval = TimeSpan.FromSeconds(3);
            var deadPeers = EnumerableExtensions.Produce(Randomizer.Int32(4, 8),
                                                        () => new KVP(ReceiverIdentities.CreateForActor(),
                                                                      new ClusterMemberMeta
                                                                      {
                                                                          ConnectionEstablished = true,
                                                                          HeartBeatInterval = heartBeatInterval,
                                                                          LastKnownHeartBeat = DateTime.UtcNow
                                                                                               - heartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion + 2)
                                                                      }));
            var activePeers = EnumerableExtensions.Produce(Randomizer.Int32(4, 8),
                                                          () => new KVP(ReceiverIdentities.CreateForActor(),
                                                                        new ClusterMemberMeta
                                                                        {
                                                                            ConnectionEstablished = true,
                                                                            HeartBeatInterval = heartBeatInterval,
                                                                            LastKnownHeartBeat = DateTime.UtcNow
                                                                                                 - heartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion - 1)
                                                                        }));
            var stalePeers = EnumerableExtensions.Produce(Randomizer.Int32(4, 8),
                                                         () => new KVP(ReceiverIdentities.CreateForActor(),
                                                                       new ClusterMemberMeta
                                                                       {
                                                                           ConnectionEstablished = false,
                                                                           HeartBeatInterval = heartBeatInterval,
                                                                           LastKnownHeartBeat = DateTime.UtcNow
                                                                                                - heartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion)
                                                                       }));
            foreach (var peer in stalePeers.Concat(activePeers).Concat(deadPeers))
            {
                peerRegistry.FindOrAdd(peer.Key, peer.Value);
            }
            //
            CollectionAssert.AreEquivalent(deadPeers.Select(p => p.Key), peerRegistry.GetPeersWithExpiredHeartBeat().Select(p => p.Key));
        }

        [Fact]
        public void GetStalePeers_ReturnsPeersWichAreNotConnectedAndLastKnownHeartBeatFromNowGreaterThanPeerIsStaleAfterTime()
        {
            var deadPeers = EnumerableExtensions.Produce(Randomizer.Int32(4, 8),
                                                        () => new KVP(ReceiverIdentities.CreateForActor(),
                                                                      new ClusterMemberMeta
                                                                      {
                                                                          ConnectionEstablished = true,
                                                                          LastKnownHeartBeat = DateTime.UtcNow
                                                                                               - config.PeerIsStaleAfter.MultiplyBy(2)
                                                                      }));
            var stalePeers = EnumerableExtensions.Produce(Randomizer.Int32(4, 8),
                                                         () => new KVP(ReceiverIdentities.CreateForActor(),
                                                                       new ClusterMemberMeta
                                                                       {
                                                                           ConnectionEstablished = false,
                                                                           LastKnownHeartBeat = DateTime.UtcNow
                                                                                                - config.PeerIsStaleAfter.MultiplyBy(2)
                                                                       }));
            var activePeers = EnumerableExtensions.Produce(Randomizer.Int32(4, 8),
                                                          () => new KVP(ReceiverIdentities.CreateForActor(),
                                                                        new ClusterMemberMeta
                                                                        {
                                                                            ConnectionEstablished = false,
                                                                            LastKnownHeartBeat = DateTime.UtcNow
                                                                        }));
            foreach (var peer in stalePeers.Concat(activePeers).Concat(deadPeers))
            {
                peerRegistry.FindOrAdd(peer.Key, peer.Value);
            }
            //
            CollectionAssert.AreEquivalent(stalePeers.Select(p => p.Key), peerRegistry.GetStalePeers().Select(p => p.Key));
        }
    }
}