using System;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ExternalRoutingTableTests
    {
        [Test]
        public void TwoExternalRegistrationsForSameMessage_AreReturnedInRoundRobinWay()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var uri1 = new Uri("tcp://127.0.0.1:40");
            var uri2 = new Uri("tcp://127.0.0.2:40");
            var peer1 = new PeerConnection {Node = new Node(uri1, socketIdentifier1.Identity)};
            var peer2 = new PeerConnection {Node = new Node(uri2, socketIdentifier2.Identity)};

            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier1, uri1);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier2, uri2);

            Assert.AreEqual(peer1.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier).Node);
            Assert.AreEqual(peer2.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier).Node);
            Assert.AreEqual(peer1.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier).Node);
        }

        [Test]
        public void RouteIsRemoved_BySocketIdentifier()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier1 = MessageIdentifier.Create<SimpleMessage>();
            var messageHandlerIdentifier2 = MessageIdentifier.Create<AsyncMessage>();
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var uri1 = new Uri("tcp://127.0.0.1:40");
            var uri2 = new Uri("tcp://127.0.0.2:40");
            var peer1 = new PeerConnection { Node = new Node(uri1, socketIdentifier1.Identity) };
            var peer2 = new PeerConnection { Node = new Node(uri2, socketIdentifier2.Identity) };

            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier1, socketIdentifier1, uri1);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier2, socketIdentifier1, uri1);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier1, socketIdentifier2, uri2);

            Assert.AreEqual(peer1.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier1).Node);

            externalRoutingTable.RemoveNodeRoute(socketIdentifier1);

            Assert.AreEqual(peer2.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier1).Node);
            Assert.AreEqual(peer2.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier1).Node);
            Assert.IsNull(externalRoutingTable.FindRoute(messageHandlerIdentifier2));
        }

        [Test]
        public void IfNoRouteRegisteredForSpecificMessage_ExternalRoutingTableReturnsNull()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = MessageIdentifier.Create<AsyncMessage>();
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, new SocketIdentifier(Guid.NewGuid().ToByteArray()), new Uri("tcp://127.0.0.1:40"));

            Assert.IsNull(externalRoutingTable.FindRoute(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void RemoveMessageRoute_RemovesOnlyProvidedMessageIdentifiers()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier1 = MessageIdentifier.Create<SimpleMessage>();
            var messageHandlerIdentifier2 = MessageIdentifier.Create<AsyncMessage>();
            var messageHandlerIdentifier3 = MessageIdentifier.Create<AsyncExceptionMessage>();
            var socketIdentifier = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var uri = new Uri("tcp://127.0.0.1:40");
            var peer = new PeerConnection { Node = new Node(uri, socketIdentifier.Identity) };
            
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier1, socketIdentifier, uri);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier2, socketIdentifier, uri);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier3, socketIdentifier, uri);

            Assert.AreEqual(peer.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier3).Node);

            externalRoutingTable.RemoveMessageRoute(new[] {messageHandlerIdentifier2, messageHandlerIdentifier3}, socketIdentifier);

            Assert.AreEqual(peer.Node, externalRoutingTable.FindRoute(messageHandlerIdentifier1).Node);
            Assert.IsNull(externalRoutingTable.FindRoute(messageHandlerIdentifier2));
            Assert.IsNull(externalRoutingTable.FindRoute(messageHandlerIdentifier3));
        }
    }
}