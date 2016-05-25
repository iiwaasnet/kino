using System;
using System.Threading;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
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
        private RouterConfiguration routerConfiguration;
        private readonly string localhost = "tcp://localhost:43";
        private ILogger logger;
        private Mock<ISocketFactory> socketFactory;
        private ActorHostSocketFactory actorHostSocketFactory;
        private const int NumberOfDealerSocketsPerActorHost = 3;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>().Object;
            actorHostSocketFactory = new ActorHostSocketFactory();
            socketFactory = new Mock<ISocketFactory>();
            socketFactory.Setup(m => m.CreateDealerSocket()).Returns(actorHostSocketFactory.CreateSocket);
            routerConfiguration = new RouterConfiguration
                                  {
                                      ScaleOutAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateIdentity()),
                                      RouterAddress = new SocketEndpoint(new Uri(localhost), SocketIdentifier.CreateIdentity())
                                  };
        }

        [Test(Description = "Assigning several actors, handling the same message type, should not thor exception.")]
        public void AssignActorWithSameInterfaceTwice_ThrowsNoException()
        {
            var actorHostManager = new ActorHostManager(socketFactory.Object, routerConfiguration, logger);

            var numberOfActors = 2;
            for (var i = 0; i < numberOfActors; i++)
            {
                Assert.DoesNotThrow(() => actorHostManager.AssignActor(new EchoActor()));
            }

            Thread.Sleep(AsyncOp);

            socketFactory.Verify(m => m.CreateDealerSocket(), Times.Exactly(NumberOfDealerSocketsPerActorHost * numberOfActors));
        }

        [Test]
        public void IfActorHostInstancePolicyIsAlwaysCreateNew_NewActorHostIsCreatedForEachActor()
        {
            var actorHostManager = new ActorHostManager(socketFactory.Object, routerConfiguration, logger);

            actorHostManager.AssignActor(new EchoActor(), ActorHostInstancePolicy.AlwaysCreateNew);
            actorHostManager.AssignActor(new ExceptionActor(), ActorHostInstancePolicy.AlwaysCreateNew);

            Thread.Sleep(AsyncOp);

            socketFactory.Verify(m => m.CreateDealerSocket(), Times.Exactly(NumberOfDealerSocketsPerActorHost * 2));
        }

        [Test]
        public void IfActorHostInstancePolicyIsTryReuseExisting_NewDifferentActorsAreHostedInOneActorHost()
        {
            var actorHostManager = new ActorHostManager(socketFactory.Object, routerConfiguration, logger);

            actorHostManager.AssignActor(new EchoActor());
            actorHostManager.AssignActor(new NullActor());

            Thread.Sleep(AsyncOp);

            socketFactory.Verify(m => m.CreateDealerSocket(), Times.Exactly(NumberOfDealerSocketsPerActorHost));
        }
    }
}