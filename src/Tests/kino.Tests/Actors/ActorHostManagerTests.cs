using System;
using System.Threading;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Security;
using kino.Core.Sockets;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Actors
{
    [TestFixture]
    public class ActorHostManagerTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(100);
        private readonly string localhost = "tcp://localhost:43";
        private ILogger logger;
        private Mock<ISocketFactory> socketFactory;
        private Mock<IPerformanceCounterManager<KinoPerformanceCounters>> performanceCounterManager;
        private ActorHostSocketFactory actorHostSocketFactory;
        private const int NumberOfDealerSocketsPerActorHost = 3;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<IRouterConfigurationProvider> routerConfigurationProvider;
        private ActorHostManager actorHostManager;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>().Object;
            actorHostSocketFactory = new ActorHostSocketFactory();
            performanceCounterManager = new Mock<IPerformanceCounterManager<KinoPerformanceCounters>>();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(actorHostSocketFactory.CreateSocket);
            var routerConfiguration = new RouterConfiguration
                                      {
                                          RouterAddress = new SocketEndpoint(new Uri("inproc://router"), SocketIdentifier.CreateIdentity())
                                      };
            routerConfigurationProvider = new Mock<IRouterConfigurationProvider>();
            routerConfigurationProvider.Setup(m => m.GetRouterConfiguration()).Returns(routerConfiguration);
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(It.IsAny<string>())).Returns(true);
            actorHostManager = new ActorHostManager(socketFactory.Object,
                                                    routerConfigurationProvider.Object,
                                                    securityProvider.Object,
                                                    performanceCounterManager.Object,
                                                    logger);
        }

        [Test(Description = "Assigning several actors, handling the same message type, should not throw exception.")]
        public void AssignActorWithSameInterfaceTwice_ThrowsNoException()
        {
            var numberOfActors = 2;
            for (var i = 0; i < numberOfActors; i++)
            {
                Assert.DoesNotThrow(() => actorHostManager.AssignActor(new EchoActor()));
            }

            AsyncOp.Sleep();

            socketFactory.Verify(m => m.CreateDealerSocket(), Times.Exactly(NumberOfDealerSocketsPerActorHost * numberOfActors));
        }

        [Test]
        public void IfActorHostInstancePolicyIsAlwaysCreateNew_NewActorHostIsCreatedForEachActor()
        {
            actorHostManager.AssignActor(new EchoActor(), ActorHostInstancePolicy.AlwaysCreateNew);
            actorHostManager.AssignActor(new ExceptionActor(), ActorHostInstancePolicy.AlwaysCreateNew);

            AsyncOp.Sleep();

            socketFactory.Verify(m => m.CreateDealerSocket(), Times.Exactly(NumberOfDealerSocketsPerActorHost * 2));
        }

        [Test]
        public void IfActorHostInstancePolicyIsTryReuseExisting_NewDifferentActorsAreHostedInOneActorHost()
        {
            actorHostManager.AssignActor(new EchoActor());
            actorHostManager.AssignActor(new NullActor());

            AsyncOp.Sleep();

            socketFactory.Verify(m => m.CreateDealerSocket(), Times.Exactly(NumberOfDealerSocketsPerActorHost));
        }
    }
}