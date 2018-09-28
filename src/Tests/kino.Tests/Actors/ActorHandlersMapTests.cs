using System;
using System.Collections.Generic;
using System.Linq;
using kino.Actors;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging.Messages;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Actors
{
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
            actorHandlersMap.Get(MessageIdentifier.Create<SimpleMessage>());

            Assert.Throws<DuplicatedKeyException>(() => { actorHandlersMap.Add(exceptionMessageActor); });

            actorHandlersMap.Get(MessageIdentifier.Create<SimpleMessage>());
            Assert.Throws<KeyNotFoundException>(() => actorHandlersMap.Get(MessageIdentifier.Create<ExceptionMessage>()));
        }

        [Test]
        public void ActorHandlersMap_CanAddTwoActorsHandlingSameMessageTypeInDifferentPartitions()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var partition = Guid.NewGuid().ToByteArray();
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
                                                                   Message = MessageDefinition.Create<SimpleMessage>(partition)
                                                               }
                                                           });

            actorHandlersMap.Add(actorWithoutPartition);
            actorHandlersMap.Add(actorWithPartition);

            actorHandlersMap.Get(MessageIdentifier.Create(typeof(SimpleMessage), partition));
            actorHandlersMap.Get(MessageIdentifier.Create(typeof(SimpleMessage)));
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

            Assert.False(actorHandlersMap.CanAdd(actor));
        }

        [Test]
        public void CanAddReturnsTrue_IfActorIsNotYetAdded()
        {
            var actorHandlersMap = new ActorHandlerMap();
            var actor = new EchoActor();

            Assert.True(actorHandlersMap.CanAdd(actor));
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