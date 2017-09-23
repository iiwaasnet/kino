using kino.Core;
using kino.Core.Framework;
using kino.Routing.ServiceMessageHandlers;
using kino.Tests.Actors.Setup;
using Moq;
using Xunit;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class ServiceMessageHandlerRegistryTests
    {
        [Fact]
        public void IfThereAreTwoMessageHandlersForTheSameMessage_DuplicatedKeyExceptionIsThrown()
        {
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandler.Setup(m => m.TargetMessage).Returns(MessageIdentifier.Create<SimpleMessage>());
            var serviceMessageHandlers = new[] {serviceMessageHandler.Object, serviceMessageHandler.Object};
            //
            Assert.Throws<DuplicatedKeyException>(() => new ServiceMessageHandlerRegistry(serviceMessageHandlers));
        }

        [Fact]
        public void IfMessageHandlerIsNotRegistered_GetMessageHandlerReturnsNull()
        {
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandler.Setup(m => m.TargetMessage).Returns(MessageIdentifier.Create<SimpleMessage>());
            var serviceMessageHandlers = new[] {serviceMessageHandler.Object};
            //
            Assert.Null(new ServiceMessageHandlerRegistry(serviceMessageHandlers).GetMessageHandler(MessageIdentifier.Create<AsyncMessage>()));
        }

        [Fact]
        public void GetMessageHandler_ReturnsReturnsRegisteredMessageHandler()
        {
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            serviceMessageHandler.Setup(m => m.TargetMessage).Returns(messageIdentifier);
            var serviceMessageHandlers = new[] {serviceMessageHandler.Object};
            //
            Assert.Equal(serviceMessageHandler.Object, new ServiceMessageHandlerRegistry(serviceMessageHandlers).GetMessageHandler(messageIdentifier));
        }
    }
}