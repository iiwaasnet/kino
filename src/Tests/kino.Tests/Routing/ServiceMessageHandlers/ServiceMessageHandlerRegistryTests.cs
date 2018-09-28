using kino.Core;
using kino.Core.Framework;
using kino.Routing.ServiceMessageHandlers;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class ServiceMessageHandlerRegistryTests
    {
        [Test]
        public void IfThereAreTwoMessageHandlersForTheSameMessage_DuplicatedKeyExceptionIsThrown()
        {
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandler.Setup(m => m.TargetMessage).Returns(MessageIdentifier.Create<SimpleMessage>());
            var serviceMessageHandlers = new[] {serviceMessageHandler.Object, serviceMessageHandler.Object};
            //
            Assert.Throws<DuplicatedKeyException>(() => new ServiceMessageHandlerRegistry(serviceMessageHandlers));
        }

        [Test]
        public void IfMessageHandlerIsNotRegistered_GetMessageHandlerReturnsNull()
        {
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            serviceMessageHandler.Setup(m => m.TargetMessage).Returns(MessageIdentifier.Create<SimpleMessage>());
            var serviceMessageHandlers = new[] {serviceMessageHandler.Object};
            //
            Assert.Null(new ServiceMessageHandlerRegistry(serviceMessageHandlers).GetMessageHandler(MessageIdentifier.Create<AsyncMessage>()));
        }

        [Test]
        public void GetMessageHandler_ReturnsReturnsRegisteredMessageHandler()
        {
            var serviceMessageHandler = new Mock<IServiceMessageHandler>();
            var messageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            serviceMessageHandler.Setup(m => m.TargetMessage).Returns(messageIdentifier);
            var serviceMessageHandlers = new[] {serviceMessageHandler.Object};
            //
            Assert.AreEqual(serviceMessageHandler.Object, new ServiceMessageHandlerRegistry(serviceMessageHandlers).GetMessageHandler(messageIdentifier));
        }
    }
}