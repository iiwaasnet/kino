using kino.Cluster;
using kino.Core;
using kino.Tests.Actors.Setup;
using Xunit;

namespace kino.Tests.Cluster
{
    public class MessageRouteTests
    {
        [Fact]
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
            Assert.Equal(first, second);
            Assert.True(first.Equals(second));
            Assert.True(first.Equals((object) second));
            Assert.True(first == second);
            Assert.False(first != second);
        }

        [Fact]
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
            Assert.NotEqual(first, second);
            Assert.False(first.Equals(second));
            Assert.False(first.Equals((object) second));
            Assert.True(first != second);
            Assert.False(first == second);
        }

        [Fact]
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
            Assert.NotEqual(first, second);
            Assert.False(first.Equals(second));
            Assert.False(first.Equals((object) second));
            Assert.True(first != second);
            Assert.False(first == second);
        }
    }
}