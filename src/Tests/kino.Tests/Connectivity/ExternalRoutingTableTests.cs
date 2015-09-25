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
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier1, new Uri("tcp://127.0.0.1:40"));
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier2, new Uri("tcp://127.0.0.2:40"));

            Assert.AreEqual(socketIdentifier1, externalRoutingTable.FindRoute(messageHandlerIdentifier));
            Assert.AreEqual(socketIdentifier2, externalRoutingTable.FindRoute(messageHandlerIdentifier));
            Assert.AreEqual(socketIdentifier1, externalRoutingTable.FindRoute(messageHandlerIdentifier));
        }


        [Test]
        public void TestRouteIsRemoved_BySocketIdentifier()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = new MessageIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier1, new Uri("tcp://127.0.0.1:40"));
            externalRoutingTable.AddMessageRoute(messageHandlerIdentifier, socketIdentifier2, new Uri("tcp://127.0.0.2:40"));

            Assert.AreEqual(socketIdentifier1, externalRoutingTable.FindRoute(messageHandlerIdentifier));

            externalRoutingTable.RemoveNodeRoute(socketIdentifier1);

            Assert.AreEqual(socketIdentifier2, externalRoutingTable.FindRoute(messageHandlerIdentifier));
            Assert.AreEqual(socketIdentifier2, externalRoutingTable.FindRoute(messageHandlerIdentifier));
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
    }
}