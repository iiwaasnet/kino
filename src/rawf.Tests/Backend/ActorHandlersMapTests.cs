using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Backend
{
    [TestFixture]
    public class ActorHandlersMapTests
    {
        [Test]
        public void TestAddingActorsHandlingTheSameMessageTwice_ThowsDuplicatedKeyException()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);
            Assert.Throws<DuplicatedKeyException>(() => { actorHandlersMap.Add(actor); });
        }

        [Test]
        public void TestGetRegisteredIdentifiers_ReturnsAllRegisteredMessageHandlers()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            var identifiers = actorHandlersMap.GetMessageHandlerIdentifiers();

            Assert.AreEqual(2, identifiers.Count());
            CollectionAssert.Contains(identifiers, new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity));
            CollectionAssert.Contains(identifiers, new MessageHandlerIdentifier(Message.CurrentVersion, AsyncMessage.MessageIdentity));

        }

        [Test]
        public void TestGettingHandlerForNonRegisteredMessageIdentifier_ThrowsKeyNotFoundException()
        {
            var actorHandlersMap = new ActorHandlersMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            Assert.Throws<KeyNotFoundException>(()=> actorHandlersMap.Get(new MessageHandlerIdentifier(Message.CurrentVersion, ExceptionMessage.MessageIdentity)));
        }
    }
}