using System;
using System.Linq;
using NUnit.Framework;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Frontend;
using rawf.Messaging;
using rawf.Messaging.Messages;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Frontend
{
    [TestFixture]
    public class CallbackHandlerStackTests
    {
        [Test]
        public void TestAddingHandlersForExistingCorrelation_ThrowsDuplicatedKeyException()
        {
            var callbackHandlerStack = new CallbackHandlerStack();

            var correlationId = new CorrelationId(Guid.NewGuid().ToByteArray());
            var promise = new Promise();
            callbackHandlerStack.Push(correlationId, promise, Enumerable.Empty<MessageHandlerIdentifier>());

            Assert.Throws<DuplicatedKeyException>(
                                                  () =>
                                                  {
                                                      callbackHandlerStack.Push(correlationId, promise, Enumerable.Empty<MessageHandlerIdentifier>());
                                                  });
        }

        [Test]
        public void TestPopCallBackHandlerForSpecificMessage_RemovesAllOtherHandlersForThisCorrelationId()
        {
            var callbackHandlerStack = new CallbackHandlerStack();

            var correlationId = new CorrelationId(Guid.NewGuid().ToByteArray());
            var promise = new Promise();
            var messageHandlerIdentifiers = new[]
                                            {
                                                new MessageHandlerIdentifier(Message.CurrentVersion, SimpleMessage.MessageIdentity),
                                                new MessageHandlerIdentifier(Message.CurrentVersion, ExceptionMessage.MessageIdentity)
                                            };
            callbackHandlerStack.Push(correlationId, promise, messageHandlerIdentifiers);

            var handler = callbackHandlerStack.Pop(new CallbackHandlerKey
                                                   {
                                                       Identity = SimpleMessage.MessageIdentity,
                                                       Version = Message.CurrentVersion,
                                                       Correlation = correlationId.Value
                                                   });

            Assert.IsNotNull(handler);

            handler = callbackHandlerStack.Pop(new CallbackHandlerKey
                                               {
                                                   Identity = ExceptionMessage.MessageIdentity,
                                                   Version = Message.CurrentVersion,
                                                   Correlation = correlationId.Value
                                               });
            Assert.IsNull(handler);
        }
    }
}