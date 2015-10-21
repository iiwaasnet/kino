using System;
using kino.Client;
using kino.Connectivity;
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
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();

            var firstKey = new CallbackHandlerKey
            {
                Version = messageIdentifier.Version,
                Identity = messageIdentifier.Identity,
                Correlation = correlationId
            };
            var secondKey = new CallbackHandlerKey
            {
                Version = messageIdentifier.Version,
                Identity = messageIdentifier.Identity,
                Correlation = correlationId
            };

            Assert.AreEqual(firstKey, secondKey);
            Assert.IsTrue(firstKey.Equals((object)secondKey));

            messageIdentifier = MessageIdentifier.Create<ExceptionMessage>();

            var thirdKey = new CallbackHandlerKey
            {
                Version = messageIdentifier.Version,
                Identity = messageIdentifier.Identity,
                Correlation = correlationId
            };
            Assert.AreNotEqual(firstKey, thirdKey);
            Assert.IsFalse(thirdKey.Equals((object)firstKey));
        }
    }
}