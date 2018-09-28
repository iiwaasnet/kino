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

        [SetUp]
        public void Setup()
        {
            config = new ClusterHealthMonitorConfiguration
                     {
                         PeerIsStaleAfter = TimeSpan.FromSeconds(2),
                         MissingHeartBeatsBeforeDeletion = 3
                     };
            peerRegistry = new ConnectedPeerRegistry(config);
        }

        [Test]
        public void IfReceiverIdentifierDoesntExist_FundReturnsNull()
        {
            var onePeer = ReceiverIdentities.CreateForActor();
            var anotherPeer = ReceiverIdentities.CreateForActor();
            peerRegistry.FindOrAdd(onePeer, new ClusterMemberMeta());
            //
            Assert.Null(peerRegistry.Find(anotherPeer));
        }

        [Test]
        public void FindOrAdd_AddsReceiverIdentityIfItDoesntExistsAndReturnsClusterMemberData()
        {
            var peer = ReceiverIdentities.CreateForActor();
            var clusterMemberMeta = new ClusterMemberMeta();
            //
            Assert.AreEqual(clusterMemberMeta, peerRegistry.FindOrAdd(peer, clusterMemberMeta));
        }

        [Test]
        public void DuplicatedReceiverIdentities_CanNotBeAdded()
        {
            var peer = ReceiverIdentities.CreateForActor();
            var clusterMemberMeta = new ClusterMemberMeta();
            //
            peerRegistry.FindOrAdd(peer, clusterMemberMeta);
            peerRegistry.FindOrAdd(peer, clusterMemberMeta);
            //
            Assert.AreEqual(1, peerRegistry.Count());
        }

        [Test]
        public void Remove_DeletesOnlyCorrespondingReceiverIdentifier()
        {
            var onePeer = ReceiverIdentities.CreateForActor();
            var anotherPeer = ReceiverIdentities.CreateForActor();
            peerRegistry.FindOrAdd(onePeer, new ClusterMemberMeta());
            peerRegistry.FindOrAdd(anotherPeer, new ClusterMemberMeta());
            Assert.AreEqual(2, peerRegistry.Count());
            //
            peerRegistry.Remove(anotherPeer);
            //
            Assert.Null(peerRegistry.Find(anotherPeer));
            Assert.NotNull(peerRegistry.Find(onePeer));
            Assert.AreEqual(1, peerRegistry.Count());
        }

        [Test]
        public void GetPeersWithExpiredHeartBeat_ReturnsPeersWichAreConnectedAndLastKnownHeartBeatFromNowGreaterThanPeerHeartBeatIntervalTimesMissingHeartBeatsBeforeDeletion()
        {
            var heartBeatInterval = TimeSpan.FromSeconds(3);
            var deadPeers = Randomizer.Int32(4, 8)
                                      .Produce(() => new KVP(ReceiverIdentities.CreateForActor(),
                                                             new ClusterMemberMeta
                                                             {
                                                                 ConnectionEstablished = true,
                                                                 HeartBeatInterval = heartBeatInterval,
                                                                 LastKnownHeartBeat = DateTime.UtcNow
                                                                                    - heartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion + 2)
                                                             }));
            var activePeers = Randomizer.Int32(4, 8)
                                        .Produce(() => new KVP(ReceiverIdentities.CreateForActor(),
                                                               new ClusterMemberMeta
                                                               {
                                                                   ConnectionEstablished = true,
                                                                   HeartBeatInterval = heartBeatInterval,
                                                                   LastKnownHeartBeat = DateTime.UtcNow
                                                                                      - heartBeatInterval.MultiplyBy(config.MissingHeartBeatsBeforeDeletion - 1)
                                                               }));
            var stalePeers = Randomizer.Int32(4, 8)
                                       .Produce(() => new KVP(ReceiverIdentities.CreateForActor(),
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

        [Test]
        public void GetStalePeers_ReturnsPeersWichAreNotConnectedAndLastKnownHeartBeatFromNowGreaterThanPeerIsStaleAfterTime()
        {
            var deadPeers = Randomizer.Int32(4, 8)
                                      .Produce(() => new KVP(ReceiverIdentities.CreateForActor(),
                                                             new ClusterMemberMeta
                                                             {
                                                                 ConnectionEstablished = true,
                                                                 LastKnownHeartBeat = DateTime.UtcNow
                                                                                    - config.PeerIsStaleAfter.MultiplyBy(2)
                                                             }));
            var stalePeers = Randomizer.Int32(4, 8)
                                       .Produce(() => new KVP(ReceiverIdentities.CreateForActor(),
                                                              new ClusterMemberMeta
                                                              {
                                                                  ConnectionEstablished = false,
                                                                  LastKnownHeartBeat = DateTime.UtcNow
                                                                                     - config.PeerIsStaleAfter.MultiplyBy(2)
                                                              }));
            var activePeers = Randomizer.Int32(4, 8)
                                        .Produce(() => new KVP(ReceiverIdentities.CreateForActor(),
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