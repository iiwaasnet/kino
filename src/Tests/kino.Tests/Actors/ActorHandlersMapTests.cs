using System;
using System.Collections.Generic;
using System.Linq;
using kino.Actors;
using kino.Core.Connectivity;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Actors
{
    [TestFixture]
    public class ActorHandlersMapTests
    {
        [Test]
        public void AddingActorWithoutRegisteredMessageHandlers_ThrowsNoException()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var voidActor = new ConfigurableActor(Enumerable.Empty<MessageHandlerDefinition>());
            actorHandlersMap.Add(voidActor);
        }

        [Test]
        public void WhenDuplicatedKeyExceptionThrown_NonOfTheActorHandlersIsAdded()
        {
            var actorHandlersMap = new ActorHandlerMap();

            var simpleMessageActor = new ConfigurableActor(new[]
                                                           {
                                                               new MessageHandlerDefinition
                                                               {
                                                                   Handler = null,
                                                                   Message = MessageDefinition.Create<SimpleMessage>()
                                                               }
                                                           });
            var exceptionMessageActor = new ConfigurableActor(new[]
                                                              {
                                                                  new MessageHandlerDefinition
                                                                  {
                                                                      Handler = null,
                                                                      Message = MessageDefinition.Create<ExceptionMessage>()
                                                                  },
                                                                  new MessageHandlerDefinition
                                                                  {
                                                                      Handler = null,
                                                                      Message = MessageDefinition.Create<SimpleMessage>()
                                                                  }
                                                              });

            actorHandlersMap.Add(simpleMessageActor);
            Assert.AreEqual(1, actorHandlersMap.GetMessageHandlerIdentifiers().Count());

            Assert.Throws<DuplicatedKeyException>(() => { actorHandlersMap.Add(exceptionMessageActor); });

            Assert.AreEqual(1, actorHandlersMap.GetMessageHandlerIdentifiers().Count());
        }

        [Test]
        public void ActorHandlersMap_CanAddTwoActorsHandlingSameMessageTypeInDifferentPartitions()
        {
            var actorHandlersMap = new ActorHandlerMap();

            var actorWithoutPartition = new ConfigurableActor(new[]
                                                           {
                                                               new MessageHandlerDefinition
                                                               {
                                                                   Handler = null,
                                                                   Message = MessageDefinition.Create<SimpleMessage>()
                                                               }
                                                           });
            var actorWithPartition = new ConfigurableActor(new[]
                                                              {
                                                                  new MessageHandlerDefinition
                                                                  {
                                                                      Handler = null,
                                                                      Message = MessageDefinition.Create<SimpleMessage>(Guid.NewGuid().ToByteArray())
                                                                  }
                                                              });

            actorHandlersMap.Add(actorWithoutPartition);
            actorHandlersMap.Add(actorWithPartition);

            Assert.AreEqual(2, actorHandlersMap.GetMessageHandlerIdentifiers().Count());
        }

        [Test]
        public void AddingActorsHandlingTheSameMessageTwice_ThowsDuplicatedKeyException()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);
            Assert.Throws<DuplicatedKeyException>(() => { actorHandlersMap.Add(actor); });
        }

        [Test]
        public void CanAddReturnsFalse_IfActorAlreadyAdded()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            Assert.IsFalse(actorHandlersMap.CanAdd(actor));
        }

        [Test]
        public void CanAddReturnsTrue_IfActorIsNotYetAdded()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            Assert.IsTrue(actorHandlersMap.CanAdd(actor));
        }

        [Test]
        public void GetRegisteredIdentifiers_ReturnsAllRegisteredMessageHandlers()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            var identifiers = actorHandlersMap.GetMessageHandlerIdentifiers();

            Assert.AreEqual(3, identifiers.Count());
            CollectionAssert.Contains(identifiers, MessageIdentifier.Create<SimpleMessage>());
            CollectionAssert.Contains(identifiers, MessageIdentifier.Create<AsyncMessage>());
            CollectionAssert.Contains(identifiers, MessageIdentifier.Create<LocalMessage>());
        }

        [Test]
        public void GettingHandlerForNonRegisteredMessageIdentifier_ThrowsKeyNotFoundException()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            actorHandlersMap.Add(actor);

            Assert.Throws<KeyNotFoundException>(() => actorHandlersMap.Get(new MessageIdentifier(KinoMessages.Exception)));
        }
    }
}