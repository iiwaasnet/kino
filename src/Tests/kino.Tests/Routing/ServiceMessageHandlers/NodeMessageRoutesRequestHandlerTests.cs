using System;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    [TestFixture]
    public class NodeMessageRoutesRequestHandlerTests
    {
        private NodeMessageRoutesRequestHandler handler;
        private Mock<ISecurityProvider> securityProvider;
        private Mock<INodeRoutesRegistrar> nodeRoutesRegistrar;
        private string domain;

        [SetUp]
        public void Setup()
        {
            securityProvider = new Mock<ISecurityProvider>();
            domain = Guid.NewGuid().ToString();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            nodeRoutesRegistrar = new Mock<INodeRoutesRegistrar>();
            handler = new NodeMessageRoutesRequestHandler(securityProvider.Object,
                                                          nodeRoutesRegistrar.Object);
        }

        [Test]
        public void IfDomainIsNotAllowed_OwnGlobalRoutesAreNotRegistered()
        {
            var message = Message.Create(new RequestNodeMessageRoutesMessage()).As<Message>();
            message.SetDomain(Guid.NewGuid().ToString());
            //
            handler.Handle(message, null);
            //
            nodeRoutesRegistrar.Verify(m => m.RegisterOwnGlobalRoutes(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void IfDomainIsAllowed_OwnGlobalRoutesAreRegistered()
        {
            var message = Message.Create(new RequestNodeMessageRoutesMessage()).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, null);
            //
            nodeRoutesRegistrar.Verify(m => m.RegisterOwnGlobalRoutes(domain), Times.Once);
        }
    }
}