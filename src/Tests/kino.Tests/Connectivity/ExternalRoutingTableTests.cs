using System;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ExternalRoutingTableTests
    {
        [Test]
        public void TestTwoExternalRegistrationsForSameMessage_AreReturnedInRoundRobinWay()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var uri1 = new Uri("tcp://127.0.0.1:40");
            var uri2 = new Uri("tcp://127.0.0.2:40");
            var node1 = new Node(uri1, socketIdentifier1.Identity);
            var node2 = new Node(uri2, socketIdentifier2.Identity);

            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier1, uri1);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier2, uri2);

            Assert.AreEqual(node1, externalRoutingTable.FindRoute(messageHandlerIdentifier));
            Assert.AreEqual(node2, externalRoutingTable.FindRoute(messageHandlerIdentifier));
            Assert.AreEqual(node1, externalRoutingTable.FindRoute(messageHandlerIdentifier));
        }


        [Test]
        public void TestRouteIsRemoved_BySocketIdentifier()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier1 = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var messageHandlerIdentifier2 = new MessageIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity);
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var uri1 = new Uri("tcp://127.0.0.1:40");
            var uri2 = new Uri("tcp://127.0.0.2:40");
            var node1 = new Node(uri1, socketIdentifier1.Identity);
            var node2 = new Node(uri2, socketIdentifier2.Identity);

            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier1, socketIdentifier1, uri1);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier2, socketIdentifier1, uri1);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier1, socketIdentifier2, uri2);


            Assert.AreEqual(node1, externalRoutingTable.FindRoute(messageHandlerIdentifier1));

            externalRoutingTable.RemoveNodeRoute(socketIdentifier1);

            Assert.AreEqual(node2, externalRoutingTable.FindRoute(messageHandlerIdentifier1));
            Assert.AreEqual(node2, externalRoutingTable.FindRoute(messageHandlerIdentifier1));
            Assert.IsNull(externalRoutingTable.FindRoute(messageHandlerIdentifier2));
        }


        [Test]
        public void TestIfNoRouteRegisteredForSpecificMessage_ExternalRoutingTableReturnsNull()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = new MessageIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, new SocketIdentifier(Guid.NewGuid().ToByteArray()), new Uri("tcp://127.0.0.1:40"));
            
            Assert.IsNull(externalRoutingTable.FindRoute(new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity)));
        }

        [Test]
        public void TestRemoveMessageRoute_RemovesOnlyProvidedMessageIdentifiers()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier1 = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var messageHandlerIdentifier2 = new MessageIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity);
            var messageHandlerIdentifier3 = new MessageIdentifier(Message.CurrentVersion, AsyncExceptionMessage.MessageIdentity);
            var socketIdentifier = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var uri = new Uri("tcp://127.0.0.1:40");
            var node = new Node(uri, socketIdentifier.Identity);

            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier1, socketIdentifier, uri);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier2, socketIdentifier, uri);
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier3, socketIdentifier, uri);

            Assert.AreEqual(node, externalRoutingTable.FindRoute(messageHandlerIdentifier3));

            externalRoutingTable.RemoveMessageRoute(new[] {messageHandlerIdentifier2, messageHandlerIdentifier3}, socketIdentifier);

            Assert.AreEqual(node, externalRoutingTable.FindRoute(messageHandlerIdentifier1));
            Assert.IsNull(externalRoutingTable.FindRoute(messageHandlerIdentifier2));
            Assert.IsNull(externalRoutingTable.FindRoute(messageHandlerIdentifier3));
        }
    }
}