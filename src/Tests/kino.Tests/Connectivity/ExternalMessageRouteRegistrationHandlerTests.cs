using System;
using System.Net.Sockets;
using System.Security;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
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
        private Mock<IClusterMembership> clusterMembership;
        private Mock<IExternalRoutingTable> externalRoutingTable;
        private RouterConfiguration config;
        private ExternalMessageRouteRegistrationHandler handler;
        private byte[] messageIdentity;
        private Mock<ISocket> socket;
        private Mock<IRouterConfigurationProvider> routerConfigurationProvider;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            socket = new Mock<ISocket>();
            externalRoutingTable = new Mock<IExternalRoutingTable>();
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(), It.IsAny<SocketIdentifier>(), It.IsAny<Uri>()))
                                .Returns(new PeerConnection {Connected = false});
            config = new RouterConfiguration {DeferPeerConnection = true};
            routerConfigurationProvider = new Mock<IRouterConfigurationProvider>();
            routerConfigurationProvider.Setup(m => m.GetRouterConfiguration()).ReturnsAsync(config);
            clusterMembership = new Mock<IClusterMembership>();
            domain = Guid.NewGuid().ToString();
            messageIdentity = Guid.NewGuid().ToByteArray();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.GetDomain(messageIdentity)).Returns(domain);
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.DomainIsAllowed(It.Is<string>(d => d != domain))).Returns(false);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            handler = new ExternalMessageRouteRegistrationHandler(externalRoutingTable.Object,
                                                                  clusterMembership.Object,
                                                                  routerConfigurationProvider.Object,
                                                                  securityProvider.Object,
                                                                  logger.Object);
        }

        [Test]
        public void IfPeerConnectionIsDeferred_NoConnectionMadeToRemotePeer()
        {
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
                                                               It.IsAny<SocketIdentifier>(),
                                                               It.IsAny<Uri>()),
                                        Times.Once);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        }

        [Test]
        public void IfPeerConnectionIsNotDeferred_ConnectionMadeToRemotePeer()
        {
            config.DeferPeerConnection = false;
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
                                                               It.IsAny<SocketIdentifier>(),
                                                               It.IsAny<Uri>()),
                                        Times.Once);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Once);
        }

        [Test]
        public void IfPeerConnectionIsNotDeferredButPeerIsAlreadyConnected_NoConnectionMadeToRemotePeer()
        {
            externalRoutingTable.Setup(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(), It.IsAny<SocketIdentifier>(), It.IsAny<Uri>()))
                                .Returns(new PeerConnection {Connected = true});
            config.DeferPeerConnection = false;

            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
                                                               It.IsAny<SocketIdentifier>(),
                                                               It.IsAny<Uri>()),
                                        Times.Once);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        }

        [Test]
        public void IfDomainIsNotAllowed_ClusterMemberIsNotAdded()
        {
            var domain = Guid.NewGuid().ToString();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Never);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
                                                               It.IsAny<SocketIdentifier>(),
                                                               It.IsAny<Uri>()),
                                        Times.Never);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
        }

        [Test]
        public void IfMessageIdentityDoesntBelongToAllowedDomain_NoConnectionMadeToRemotePeer()
        {
            var messageIdentity = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity,
                                                                        Version = Guid.NewGuid().ToByteArray()
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Never);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
                                                               It.IsAny<SocketIdentifier>(),
                                                               It.IsAny<Uri>()),
                                        Times.Never);
            socket.Verify(m => m.Connect(It.IsAny<Uri>()), Times.Never);
            logger.Verify(m => m.Warn(It.IsAny<object>()), Times.Once);
        }

        [Test]
        public void IfDomainIsAllowed_MessageHubIdentityAlwaysAddedToClusterMembers()
        {
            var messageIdentity = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new RegisterExternalMessageRouteMessage
                                         {
                                             Uri = "tcp://127.0.0.1:80",
                                             MessageContracts = new[]
                                                                {
                                                                    new MessageContract
                                                                    {
                                                                        Identity = messageIdentity
                                                                    }
                                                                },
                                             SocketIdentity = Guid.NewGuid().ToByteArray()
                                         },
                                         domain);
            //
            handler.Handle(message, socket.Object);
            //
            clusterMembership.Verify(m => m.AddClusterMember(It.IsAny<SocketEndpoint>()), Times.Once);
            externalRoutingTable.Verify(m => m.AddMessageRoute(It.IsAny<MessageIdentifier>(),
                                                               It.IsAny<SocketIdentifier>(),
                                                               It.IsAny<Uri>()),
                                        Times.Once);
        }
    }
}