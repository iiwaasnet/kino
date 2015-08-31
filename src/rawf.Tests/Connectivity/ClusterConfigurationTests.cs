using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Connectivity;
using rawf.Diagnostics;

namespace rawf.Tests.Connectivity
{
    [TestFixture]
    public class ClusterConfigurationTests
    {
        private ILogger logger;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>().Object;
        }

        [Test]
        public void TestAddClusterMember()
        {
            var config = new ClusterConfiguration(logger);
            var localhost = "tcp://127.0.0.1:40";
            var ep1 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());
            var ep2 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());
            config.AddClusterMember(ep1);
            config.AddClusterMember(ep2);

            CollectionAssert.Contains(config.GetClusterMembers(), ep1);
            CollectionAssert.Contains(config.GetClusterMembers(), ep2);
        }

        [Test]
        public void TestDeleteClusterMember()
        {
            var config = new ClusterConfiguration(logger);
            var localhost = "tcp://127.0.0.1:40";
            var ep1 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());
            var ep2 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());
            config.AddClusterMember(ep1);
            config.AddClusterMember(ep2);

            CollectionAssert.Contains(config.GetClusterMembers(), ep1);
            CollectionAssert.Contains(config.GetClusterMembers(), ep2);

            config.DeleteClusterMember(ep1);

            CollectionAssert.DoesNotContain(config.GetClusterMembers(), ep1);
            CollectionAssert.Contains(config.GetClusterMembers(), ep2);


        }

        [Test]
        public void TestNodeConsideredDead_IfLastKnownPongWasLongerThanPongSilenceBeforeRouteDeletionAgo()
        {
            var config = new ClusterConfiguration(logger)
                         {
                             PongSilenceBeforeRouteDeletion = TimeSpan.FromSeconds(2)
                         };
            var localhost = "tcp://127.0.0.1:40";
            var ep1 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());
            var ep2 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());
            config.AddClusterMember(ep1);
            config.AddClusterMember(ep2);

            Thread.Sleep(config.PongSilenceBeforeRouteDeletion);

            config.KeepAlive(ep1);

            CollectionAssert.Contains(config.GetDeadMembers(), ep2);
            CollectionAssert.DoesNotContain(config.GetDeadMembers(), ep1);
        }
    }
}