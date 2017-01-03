using System;
using System.Linq;
using System.Security;
using kino.Cluster;
using kino.Connectivity;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ExternalMessageRouteRegistrationHandlerTests
    {
        private Mock<ILogger> logger;
        private Mock<ISecurityProvider> securityProvider;
        private string domain;
        private Mock<IExternalRoutingTable> externalRoutingTable;
        private ExternalMessageRouteRegistrationHandler handler;
        private Mock<ISocket> socket;
        private Mock<IClusterHealthMonitor> clusterHealthMonitor;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            socket = new Mock<ISocket>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<ExternalRouteRegistration>()))
                                .Returns(new PeerConnection {Connected = false});

            domain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.GetDomain(It.IsAny<byte[]>())).Returns(domain);
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.DomainIsAllowed(It.Is<string>(d => d != domain))).Returns(false);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            clusterHealthMonitor = new Mock<IClusterHealthMonitor>();
            handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable.Object,
                                                                  securityProvider.Object,
                                                                  clusterHealthMonitor.Object,
                                                                  logger.Object);
        }

        [Test]
        public void PeerAddedToClusterHealthMonitor_OnlyOnce()
        {
            var payload = new RegisterExternalMessageRouteMessage
                          {
                              Uri = "tcp://127.0.0.1:80",
                              NodeIdentity = Guid.NewGuid().ToByteArray(),
                              Health = new kino.Messaging.Messages.Health
                                       {
                                           Uri = "tcp://127.0.0.1:812",
                                           HeartBeatInterval = TimeSpan.FromSeconds(4)
                                       },
                              Routes = new[]
                                       {
                                           new RouteRegistration
                                           {
                                               ReceiverIdentity = ReceiverIdentities.CreateForActor().Identity,
                                               MessageContracts = EnumerableExtenions.Produce(Randomizer.UInt16(2, 5),
                                                                                              () => new kino.Messaging.Messages.MessageContract
                                                                                                    {
                                                                                                        Identity = Guid.NewGuid().ToByteArray(),
                                                                                                        Version = Randomizer.UInt16()
                                                                                                    })
                                                                                     .ToArray()
                                           }
                                       }
                          };
            var message = Message.Create(payload).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, socket.Object);
            //
            Func<Node, bool> isThisPeer = p => p.Uri.ToSocketAddress() == payload.Uri
                                               && Unsafe.ArraysEqual(p.SocketIdentity, payload.NodeIdentity);
            clusterHealthMonitor.Verify(m => m.AddPeer(It.Is<Node>(p => isThisPeer(p)), It.IsAny<Cluster.Health>()), Times.Once);
        }

        //[Test]
        //public void IfPeerConnectionIsNotDeferred_ConnectionMadeToRemotePeer()
        //{
        //    config.DeferPeerConnection = false;
        //    var message = Message.Create(new RegisterExternalMessageRouteMessage
        //                                 {
        //                                     Uri = "tcp://127.0.0.1:80",
        //                                     MessageContracts = new[]
        //                                                        {
        //                                                            new MessageContract
        //                                                            {
        //                                                                Identity = messageIdentity,
        //                                                                Version = Guid.NewGuid().ToByteArray()
        //                                                            }
        //                                                        },
        //                                     SocketIdentity = Guid.NewGuid().ToByteArray()
        //                                 },
        //                                 domain);
        //    //
        //    handler.Handle(message, socket.Object);
        //    //
        //    clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
        //    externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
        //                                                       It.IsAny<SocketIdentifier>(),
        //                                                       It.IsAny<Uri>()),
        //                                Times.Once);
        //    socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Once);
        //}

        //[Test]
        //public void IfPeerConnectionIsNotDeferredButPeerIsAlreadyConnected_NoConnectionMadeToRemotePeer()
        //{
        //    externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(), It.IsAny<SocketIdentifier>(), It.IsAny<Uri>()))
        //                        .Returns(new PeerConnection {Connected = true});
        //    config.DeferPeerConnection = false;

        //    var message = Message.Create(new RegisterExternalMessageRouteMessage
        //                                 {
        //                                     Uri = "tcp://127.0.0.1:80",
        //                                     MessageContracts = new[]
        //                                                        {
        //                                                            new MessageContract
        //                                                            {
        //                                                                Identity = messageIdentity,
        //                                                                Version = Guid.NewGuid().ToByteArray()
        //                                                            }
        //                                                        },
        //                                     SocketIdentity = Guid.NewGuid().ToByteArray()
        //                                 },
        //                                 domain);
        //    //
        //    handler.Handle(message, socket.Object);
        //    //
        //    clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
        //    externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
        //                                                       It.IsAny<SocketIdentifier>(),
        //                                                       It.IsAny<Uri>()),
        //                                Times.Once);
        //    socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        //}

        //[Test]
        //public void IfDomainIsNotAllowed_ClusterMemberIsNotAdded()
        //{
        //    var domain = Guid.NewGuid().ToString();
        //    var message = Message.Create(new RegisterExternalMessageRouteMessage
        //                                 {
        //                                     Uri = "tcp://127.0.0.1:80",
        //                                     MessageContracts = new[]
        //                                                        {
        //                                                            new MessageContract
        //                                                            {
        //                                                                Identity = messageIdentity,
        //                                                                Version = Guid.NewGuid().ToByteArray()
        //                                                            }
        //                                                        },
        //                                     SocketIdentity = Guid.NewGuid().ToByteArray()
        //                                 },
        //                                 domain);
        //    //
        //    handler.Handle(message, socket.Object);
        //    //
        //    clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Never);
        //    externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
        //                                                       It.IsAny<SocketIdentifier>(),
        //                                                       It.IsAny<Uri>()),
        //                                Times.Never);
        //    socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        //}

        //[Test]
        //public void IfMessageIdentityDoesntBelongToAllowedDomain_NoConnectionMadeToRemotePeer()
        //{
        //    var messageIdentity = Guid.NewGuid().ToByteArray();
        //    var message = Message.Create(new RegisterExternalMessageRouteMessage
        //                                 {
        //                                     Uri = "tcp://127.0.0.1:80",
        //                                     MessageContracts = new[]
        //                                                        {
        //                                                            new MessageContract
        //                                                            {
        //                                                                Identity = messageIdentity,
        //                                                                Version = Guid.NewGuid().ToByteArray()
        //                                                            }
        //                                                        },
        //                                     SocketIdentity = Guid.NewGuid().ToByteArray()
        //                                 },
        //                                 domain);
        //    //
        //    handler.Handle(message, socket.Object);
        //    //
        //    clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Never);
        //    externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
        //                                                       It.IsAny<SocketIdentifier>(),
        //                                                       It.IsAny<Uri>()),
        //                                Times.Never);
        //    socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        //    logger.Verify(m => m.Warn(It.IsAny<object>()), Times.Once);
        //}

        //[Test]
        //public void IfDomainIsAllowed_MessageHubIdentityAlwaysAddedToClusterMembers()
        //{
        //    var messageIdentity = Guid.NewGuid().ToByteArray();
        //    var message = Message.Create(new RegisterExternalMessageRouteMessage
        //                                 {
        //                                     Uri = "tcp://127.0.0.1:80",
        //                                     MessageContracts = new[]
        //                                                        {
        //                                                            new MessageContract
        //                                                            {
        //                                                                Identity = messageIdentity
        //                                                            }
        //                                                        },
        //                                     SocketIdentity = Guid.NewGuid().ToByteArray()
        //                                 },
        //                                 domain);
        //    //
        //    handler.Handle(message, socket.Object);
        //    //
        //    clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
        //    externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
        //                                                       It.IsAny<SocketIdentifier>(),
        //                                                       It.IsAny<Uri>()),
        //                                Times.Once);
        //}
    }
}