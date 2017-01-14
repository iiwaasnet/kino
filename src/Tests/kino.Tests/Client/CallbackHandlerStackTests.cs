using System.Linq;
using kino.Client;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging.Messages;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Client
{
    [TestFixture]
    public class CallbackHandlerStackTests
    {
        private CallbackHandlerStack callbackHandlerStack;

        [SetUp]
        public void Setup()
        {
            callbackHandlerStack = new CallbackHandlerStack();
        }

        [Test]
        public void PushCallbackWithSamePromiseTwice_ThrowsDuplicatedKeyException()
        {
            var promise = new Promise(Randomizer.Int64());
            callbackHandlerStack.Push(promise, Enumerable.Empty<MessageIdentifier>());
            //
            Assert.Throws<DuplicatedKeyException>(() => { callbackHandlerStack.Push(promise, Enumerable.Empty<MessageIdentifier>()); });
        }

        [Test]
        public void PopCallBackHandlerForSpecificCallbackKey_RemovesAllOtherHandlersForThisCallbackKey()
        {
            var promise = new Promise(Randomizer.Int64());
            var simpleMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var messageHandlerIdentifiers = new[]
                                            {
                                                simpleMessageIdentifier,
                                                MessageIdentifier.Create<ExceptionMessage>()
                                            };
            callbackHandlerStack.Push(promise, messageHandlerIdentifiers);
            //
            var handler = callbackHandlerStack.Pop(new CallbackHandlerKey
                                                   {
                                                       Identity = simpleMessageIdentifier.Identity,
                                                       Version = simpleMessageIdentifier.Version,
                                                       Partition = simpleMessageIdentifier.Partition,
                                                       CallbackKey = promise.CallbackKey.Value
                                                   });

            Assert.IsNotNull(handler);

            handler = callbackHandlerStack.Pop(new CallbackHandlerKey
                                               {
                                                   Identity = KinoMessages.Exception.Identity,
                                                   Version = KinoMessages.Exception.Version,
                                                   CallbackKey = promise.CallbackKey.Value
                                               });
            Assert.IsNull(handler);
        }
    }
}