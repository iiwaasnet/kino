using System;
using System.Linq;
using kino.Client;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Client
{
    [TestFixture]
    public class CallbackHandlerStackTests
    {
        [Test]
        public void AddingHandlersForExistingCorrelation_ThrowsDuplicatedKeyException()
        {
            var callbackHandlerStack = new CallbackHandlerStack();

            var correlationId = new CorrelationId(Guid.NewGuid().ToByteArray());
            var promise = new Promise();
            callbackHandlerStack.Push(correlationId, promise, Enumerable.Empty<MessageIdentifier>());

            Assert.Throws<DuplicatedKeyException>(() => { callbackHandlerStack.Push(correlationId, promise, Enumerable.Empty<MessageIdentifier>()); });
        }

        [Test]
        public void PopCallBackHandlerForSpecificMessage_RemovesAllOtherHandlersForThisCorrelationId()
        {
            var callbackHandlerStack = new CallbackHandlerStack();

            var correlationId = new CorrelationId(Guid.NewGuid().ToByteArray());
            var promise = new Promise();
            var simpleMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var messageHandlerIdentifiers = new[]
                                            {
                                                simpleMessageIdentifier,
                                                MessageIdentifier.Create<ExceptionMessage>()
                                            };
            callbackHandlerStack.Push(correlationId, promise, messageHandlerIdentifiers);

            var handler = callbackHandlerStack.Pop(new CallbackHandlerKey
                                                   {
                                                       Identity = simpleMessageIdentifier.Identity,
                                                       Version = simpleMessageIdentifier.Version,
                                                       Partition = simpleMessageIdentifier.Partition,
                                                       Correlation = correlationId.Value
                                                   });

            Assert.IsNotNull(handler);

            handler = callbackHandlerStack.Pop(new CallbackHandlerKey
                                               {
                                                   Identity = KinoMessages.Exception.Identity,
                                                   Version = KinoMessages.Exception.Version,
                                                   Correlation = correlationId.Value
                                               });
            Assert.IsNull(handler);
        }
    }
}