using kino.Cluster;
using kino.Core;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Cluster
{
    [TestFixture]
    public class MessageRouteTests
    {
        [Test]
        public void TwoMessageRoutesAreEqual_IfTheirMessageAndReceiverPropertiesAreEqual()
        {
            var receiver = ReceiverIdentities.CreateForActor();
            var first = new MessageRoute
                        {
                            Message = MessageIdentifier.Create<SimpleMessage>(),
                            Receiver = receiver
                        };
            var second = new MessageRoute
                         {
                             Message = MessageIdentifier.Create<SimpleMessage>(),
                             Receiver = receiver
                         };
            //
            Assert.AreEqual(first, second);
            Assert.IsTrue(first.Equals(second));
            Assert.IsTrue(first.Equals((object) second));
            Assert.IsTrue(first == second);
            Assert.IsFalse(first != second);
        }

        [Test]
        public void TwoMessageRoutesAreNotEqual_IfTheirMessagePropertiesAreNotEqual()
        {
            var receiver = ReceiverIdentities.CreateForActor();
            var first = new MessageRoute
                        {
                            Message = MessageIdentifier.Create<NullMessage>(),
                            Receiver = receiver
                        };
            var second = new MessageRoute
                         {
                             Message = MessageIdentifier.Create<SimpleMessage>(),
                             Receiver = receiver
                         };
            //
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(first.Equals((object) second));
            Assert.IsTrue(first != second);
            Assert.IsFalse(first == second);
        }

        [Test]
        public void TwoMessageRoutesAreNotEqual_IfTheirReceiverPropertiesAreNotEqual()
        {
            var first = new MessageRoute
                        {
                            Message = MessageIdentifier.Create<SimpleMessage>(),
                            Receiver = ReceiverIdentities.CreateForActor()
                        };
            var second = new MessageRoute
                         {
                             Message = MessageIdentifier.Create<SimpleMessage>(),
                             Receiver = ReceiverIdentities.CreateForActor()
                         };
            //
            Assert.AreNotEqual(first, second);
            Assert.IsFalse(first.Equals(second));
            Assert.IsFalse(first.Equals((object) second));
            Assert.IsTrue(first != second);
            Assert.IsFalse(first == second);
        }
    }
}