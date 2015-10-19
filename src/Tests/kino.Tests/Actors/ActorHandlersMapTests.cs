using System.Collections.Generic;
using System.Linq;
using kino.Actors;
using kino.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Tests.Actors.Setup;
using NUnit.Framework;
using MessageIdentifier = kino.Connectivity.MessageIdentifier;

namespace kino.Tests.Actors
{
    [TestFixture]
    public class ActorHandlersMapTests
    {
        [Test]
        public void TestAddingActorsHandlingTheSameMessageTwice_ThowsDuplicatedKeyException()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);
            Assert.Throws<DuplicatedKeyException>(() => { actorHandlersMap.Add(actor); });
        }

        [Test]
        public void TestGetRegisteredIdentifiers_ReturnsAllRegisteredMessageHandlers()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            var identifiers = actorHandlersMap.GetMessageHandlerIdentifiers();

            Assert.AreEqual(2, identifiers.Count());
            CollectionAssert.Contains(identifiers, MessageIdentifier.Create<SimpleMessage>());
            CollectionAssert.Contains(identifiers, MessageIdentifier.Create<AsyncMessage>());

        }

        [Test]
        public void TestGettingHandlerForNonRegisteredMessageIdentifier_ThrowsKeyNotFoundException()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            Assert.Throws<KeyNotFoundException>(()=> actorHandlersMap.Get(new MessageIdentifier(Message.CurrentVersion, ExceptionMessage.MessageIdentity)));
        }
    }
}