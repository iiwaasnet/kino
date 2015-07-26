using System;
using NUnit.Framework;
using rawf.Messaging;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Messaging
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void TestFlowStartMessage_HasCorrelationIdSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            Assert.IsNotNull(message.CorrelationId);
            CollectionAssert.IsNotEmpty(message.CorrelationId);
        }

        [Test]
        public void TestMessage_HasVersionSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            Assert.IsNotNull(message.Version);
            CollectionAssert.AreEqual(Message.CurrentVersion, message.Version);
        }

        [Test]
        public void TestMessage_HasIdentitySet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            Assert.IsNotNull(message.Identity);
            CollectionAssert.AreEqual(SimpleMessage.MessageIdentity, message.Identity);
        }

        [Test]
        public void TestMessage_HasDistributionPatternSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            CollectionAssert.Contains(Enum.GetValues(typeof(DistributionPattern)), message.Distribution);
        }
    }
}