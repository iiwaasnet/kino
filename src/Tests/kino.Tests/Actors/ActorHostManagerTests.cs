using kino.Actors;
using kino.Core.Diagnostics;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Actors
{
    public class ActorHostManagerTests
    {
        private ILogger logger;
        private ActorHostManager actorHostManager;
        private Mock<IActorHostFactory> actorHostFactory;
        private Mock<IActorHost> actorHost;

        [SetUp]
        public void Setup()
        {
            logger = new Mock<ILogger>().Object;
            actorHostFactory = new Mock<IActorHostFactory>();
            actorHost = new Mock<IActorHost>();
            actorHostFactory.Setup(m => m.Create()).Returns(actorHost.Object);
            actorHostManager = new ActorHostManager(actorHostFactory.Object, logger);
        }

        [Test(Description = "Assigning several actors, handling the same message type, should not throw exception.")]
        public void AssignActorWithSameInterfaceTwice_ThrowsNoException()
        {
            var numberOfActors = 2;
            for (var i = 0; i < numberOfActors; i++)
            {
                actorHostManager.AssignActor(new EchoActor());
            }
            //
            actorHostFactory.Verify(m => m.Create(), Times.Exactly(numberOfActors));
        }

        [Test]
        public void IfActorHostInstancePolicyIsAlwaysCreateNew_NewActorHostIsCreatedForEachActor()
        {
            actorHostManager.AssignActor(new EchoActor(), ActorHostInstancePolicy.AlwaysCreateNew);
            actorHostManager.AssignActor(new ExceptionActor(), ActorHostInstancePolicy.AlwaysCreateNew);
            //
            actorHostFactory.Verify(m => m.Create(), Times.Exactly(2));
        }

        [Test]
        public void IfActorHostCanHostAllActorsBeingAssigned_ThisActorHostInstanceIsUsed()
        {
            var echoActor = new EchoActor();
            var nullActor = new NullActor();
            actorHost.Setup(m => m.CanAssignActor(nullActor)).Returns(true);
            actorHost.Setup(m => m.CanAssignActor(echoActor)).Returns(true);
            //
            actorHostManager.AssignActor(echoActor);
            actorHostManager.AssignActor(nullActor);
            //
            actorHostFactory.Verify(m => m.Create(), Times.Once);
        }
    }
}