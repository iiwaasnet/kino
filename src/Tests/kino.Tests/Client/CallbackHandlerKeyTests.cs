using System;
using kino.Client;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Client
{
    [TestFixture]
    public class CallbackHandlerKeyTests
    {
        [Test]
        public void TestTwoCallbackHandlerKeies_AreComparedByVersionIdentityCorrelation()
        {
            var correlationId = Guid.NewGuid().ToByteArray();

            var firstKey = new CallbackHandlerKey
                           {
                               Version = Message.CurrentVersion,
                               Identity = SimpleMessage.MessageIdentity,
                               Correlation = correlationId
                           };
            var secondKey = new CallbackHandlerKey
                            {
                                Version = Message.CurrentVersion,
                                Identity = SimpleMessage.MessageIdentity,
                                Correlation = correlationId
                            };

            Assert.AreEqual(firstKey, secondKey);
            Assert.IsTrue(firstKey.Equals((object) secondKey));

            var thirdKey = new CallbackHandlerKey
                           {
                               Version = Message.CurrentVersion,
                               Identity = ExceptionMessage.MessageIdentity,
                               Correlation = correlationId
                           };
            Assert.AreNotEqual(firstKey, thirdKey);
            Assert.IsFalse(thirdKey.Equals((object) firstKey));
        }
    }
}