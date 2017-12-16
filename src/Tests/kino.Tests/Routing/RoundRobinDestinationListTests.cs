using System;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Routing;
using Moq;
using Xunit;

namespace kino.Tests.Routing
{
    public class RoundRobinDestinationListTests
    {
        private readonly RoundRobinDestinationList roundRobinList;
        private readonly Mock<ILogger> logger;

        public RoundRobinDestinationListTests()
        {
            logger = new Mock<ILogger>();
            roundRobinList = new RoundRobinDestinationList(logger.Object);
        }

        [Fact]
        public void DestinationIsAddedOnlyOnce()
        {
            var node = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            //
            roundRobinList.Add(node);
            roundRobinList.Add(node);
            //
            Assert.Equal(node, roundRobinList.SelectNextDestination(node));
            Assert.Equal(node, roundRobinList.SelectNextDestination(node));
        }

        [Fact]
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
            Assert.Equal(node2, roundRobinList.SelectNextDestination(node2));
        }

        [Fact]
        public void IfDestinationIsNotFound_SelectNextDestinationReturnsReturnsDestinationItself()
        {
            var node = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            //
            Assert.Equal(node, roundRobinList.SelectNextDestination(node));
            logger.Verify(m => m.Warn(It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public void SelectNextDestinationReturns_IfInputIsNull()
        {
            Assert.Null(roundRobinList.SelectNextDestination(null, null));
        }

        [Fact]
        public void Destinations_ReturnedRoundRobin()
        {
            var node1 = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            var node2 = new Node("tcp://127.0.0.1", Guid.NewGuid().ToByteArray());
            roundRobinList.Add(node1);
            roundRobinList.Add(node2);
            //
            for (var i = 0; i < 4; i++)
            {
                Assert.Equal(i % 2 == 0
                                 ? node1
                                 : node2,
                             roundRobinList.SelectNextDestination(node1, node2));
            }
        }
    }
}