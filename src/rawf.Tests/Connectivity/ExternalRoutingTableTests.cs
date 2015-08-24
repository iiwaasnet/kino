using System;
using Moq;
using NUnit.Framework;
using rawf.Connectivity;
using rawf.Diagnostics;
using rawf.Messaging;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Connectivity
{
    [TestFixture]
    public class ExternalRoutingTableTests
    {
        [Test]
        public void TestTwoExternalRegistrationsForSameMessage_AreReturnedInRoundRobinWay()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            externalRoutingTable.Push(messageHandlerIdentifier, socketIdentifier1, new Uri("tcp://127.0.0.1:40"));
            externalRoutingTable.Push(messageHandlerIdentifier, socketIdentifier2, new Uri("tcp://127.0.0.2:40"));

            Assert.AreEqual(socketIdentifier1, externalRoutingTable.Pop(messageHandlerIdentifier));
            Assert.AreEqual(socketIdentifier2, externalRoutingTable.Pop(messageHandlerIdentifier));
            Assert.AreEqual(socketIdentifier1, externalRoutingTable.Pop(messageHandlerIdentifier));
        }


        [Test]
        public void TestRouteIsRemoved_BySocketIdentifier()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity);
            var socketIdentifier1 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            var socketIdentifier2 = new SocketIdentifier(Guid.NewGuid().ToByteArray());
            externalRoutingTable.Push(messageHandlerIdentifier, socketIdentifier1, new Uri("tcp://127.0.0.1:40"));
            externalRoutingTable.Push(messageHandlerIdentifier, socketIdentifier2, new Uri("tcp://127.0.0.2:40"));

            Assert.AreEqual(socketIdentifier1, externalRoutingTable.Pop(messageHandlerIdentifier));

            externalRoutingTable.RemoveRoute(socketIdentifier1);

            Assert.AreEqual(socketIdentifier2, externalRoutingTable.Pop(messageHandlerIdentifier));
            Assert.AreEqual(socketIdentifier2, externalRoutingTable.Pop(messageHandlerIdentifier));
        }


        [Test]
        public void TestIfNoRouteRegisteredForSpecificMessage_ExternalRoutingTableReturnsNull()
        {
            var logger = new Mock<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger.Object);
            var messageHandlerIdentifier = new MessageHandlerIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity);
            externalRoutingTable.Push(messageHandlerIdentifier, new SocketIdentifier(Guid.NewGuid().ToByteArray()), new Uri("tcp://127.0.0.1:40"));
            
            Assert.IsNull(externalRoutingTable.Pop(new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity)));
        }
    }
}