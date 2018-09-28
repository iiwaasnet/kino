using System;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Routing;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Routing
{
    public class RoundRobinDestinationListTests
    {
        private RoundRobinDestinationList roundRobinList;
        private Mock<ILogger> logger;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>();
            roundRobinList = new RoundRobinDestinationList(logger.Object);
        }

        [Test]
        public void DestinationIsAddedOnlyOnce()
        {
            var node = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            //
            roundRobinList.Add(node);
            roundRobinList.Add(node);
            //
            Assert.AreEqual(node, roundRobinList.SelectNextDestination(node));
            Assert.AreEqual(node, roundRobinList.SelectNextDestination(node));
        }

        [Test]
        public void Remove_RemovesOnlySpecificDestination()
        {
            var node1 = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            var node2 = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            roundRobinList.Add(node1);
            roundRobinList.Add(node2);
            //
            roundRobinList.Remove(node1);
            roundRobinList.Remove(node1);
            //
            Assert.AreEqual(node2, roundRobinList.SelectNextDestination(node2));
        }

        [Test]
        public void IfDestinationIsNotFound_SelectNextDestinationReturnsReturnsDestinationItself()
        {
            var node = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            //
            Assert.AreEqual(node, roundRobinList.SelectNextDestination(node));
            logger.Verify(m => m.Warn(It.IsAny<object>()), Times.Once);
        }

        [Test]
        public void SelectNextDestinationReturns_IfInputIsNull()
        {
            Assert.Null(roundRobinList.SelectNextDestination(null, null));
        }

        [Test]
        public void Destinations_ReturnedRoundRobin()
        {
            var node1 = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            var node2 = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            roundRobinList.Add(node1);
            roundRobinList.Add(node2);
            //
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(i % 2 == 0
                                 ? node1
                                 : node2,
                             roundRobinList.SelectNextDestination(node1, node2));
            }
        }
    }
}