using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Security;
using kino.Core.Sockets;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class ClusterMessageSenderTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private ClusterMessageSender clusterMessageSender;
        private Mock<IRendezvousCluster> rendezvousCluster;
        private Mock<ISocketFactory> socketFactory;
        private ClusterMonitorSocketFactory clusterMonitorSocketFactory;
        private Mock<ILogger> logger;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<IRouterConfigurationProvider> routerConfigurationProvider;

        [SetUp]
        public void Setup()
        {
            clusterMonitorSocketFactory = new ClusterMonitorSocketFactory();
            logger = new Mock<ILogger>();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(clusterMonitorSocketFactory.CreateSocket);
            rendezvousCluster = new Mock<IRendezvousCluster>();

            var rendezvousEndpoint = new RendezvousEndpoint(new Uri("tcp://127.0.0.1:5000"),
                                                            new Uri("tcp://127.0.0.1:5000"));
            rendezvousCluster.Setup(m => m.GetCurrentRendezvousServer()).Returns(rendezvousEndpoint);
            securityProvider = new Mock<ISecurityProvider>();
            var routerConfiguration = new RouterConfiguration
                                      {
                                          RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateIdentity())
                                      };
            var scaleOutAddress = new SocketEndpoint(new Uri("tcp://127.0.0.1:5000"), SocketIdentifier.CreateIdentity());
            routerConfigurationProvider = new Mock<IRouterConfigurationProvider>();
            routerConfigurationProvider.Setup(m => m.GetRouterConfiguration()).Returns(routerConfiguration);
            routerConfigurationProvider.Setup(m => m.GetScaleOutAddress()).Returns(scaleOutAddress);
            clusterMessageSender = new ClusterMessageSender(rendezvousCluster.Object,
                                                            routerConfigurationProvider.Object,
                                                            socketFactory.Object,
                                                            performanceCounterManager.Object,
                                                            securityProvider.Object,
                                                            logger.Object);
        }

        [Test]
        public async void WhenClusterMessageSenderStops_UnregisterNodeMessageIsSentOncePerEachAllowedDomain()
        {
            var allowedDomains = new[] {Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
            foreach (var domain in allowedDomains)
            {
                securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var task = Task.Factory.StartNew(() => clusterMessageSender.StartBlockingSendMessages(cancellationTokenSource.Token, new Barrier(1)));
            cancellationTokenSource.CancelAfter(AsyncOp);

            await task;

            var socket = clusterMonitorSocketFactory.GetClusterMonitorSendingSocket();
            var messages = socket.GetSentMessages().BlockingAll(AsyncOp);

            Assert.AreEqual(allowedDomains.Count(), messages.Count(m => Unsafe.Equals(m.Identity, KinoMessages.UnregisterNode.Identity)));
            //NOTE: REQCLUSTROUTES will also be sent for each allowedDomains. That's why the following condition is wrong until RequestClusterRoutes() call is removed
            //Assert.IsTrue(messages.All(m => Unsafe.Equals(m.Identity, KinoMessages.UnregisterNode.Identity)));
            Assert.IsTrue(messages.All(m => allowedDomains.Contains(m.Domain)));
        }


        //TODO: Implement the below test here
        //[Test]
        //public void RequestClusterMessageRoutesMessage_IsSentOncePerEachAllowedDomain()
        //{
        //    var allowedDomains = new[] {domain, Guid.NewGuid().ToString(), Guid.NewGuid().ToString()};
        //    securityProvider.Setup(m => m.GetAllowedDomains()).Returns(allowedDomains);
        //    var clusterMessageSender = new Mock<IClusterMessageSender>();
        //    var clusterMonitor = new ClusterMonitor(routerConfigurationProvider.Object,
        //                                            clusterMembership.Object,
        //                                            clusterMessageSender.Object,
        //                                            clusterMessageListener,
        //                                            routeDiscovery.Object,
        //                                            securityProvider.Object);
        //    //
        //    clusterMonitor.RequestClusterRoutes();
        //    //
        //    clusterMessageSender.Verify(m => m.EnqueueMessage(It.Is<IMessage>(msg => allowedDomains.Contains(msg.Domain))), Times.Exactly(allowedDomains.Length));
        //}
    }
}