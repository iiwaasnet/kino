using System;
using System.Security;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;
using Moq;
using Xunit;

namespace kino.Tests.Routing.ServiceMessageHandlers
{
    public class ClusterMessageRoutesRequestHandlerTests
    {
        private readonly string domain;
        private readonly Mock<ISecurityProvider> securityProvider;
        private readonly ClusterMessageRoutesRequestHandler handler;
        private readonly Mock<INodeRoutesRegistrar> nodeRoutesRegistrar;

        public ClusterMessageRoutesRequestHandlerTests()
        {
            domain = Guid.NewGuid().ToString();
            securityProvider = new Mock<ISecurityProvider>();
            securityProvider.Setup(m => m.DomainIsAllowed(domain)).Returns(true);
            securityProvider.Setup(m => m.DomainIsAllowed(It.Is<string>(d => d != domain))).Returns(false);
            securityProvider.Setup(m => m.GetAllowedDomains()).Returns(new[] {domain});
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(domain, It.IsAny<byte[]>())).Returns(new byte[0]);
            securityProvider.As<ISignatureProvider>().Setup(m => m.CreateSignature(It.Is<string>(d => d != domain), It.IsAny<byte[]>())).Throws<SecurityException>();
            nodeRoutesRegistrar = new Mock<INodeRoutesRegistrar>();
            handler = new ClusterMessageRoutesRequestHandler(securityProvider.Object,
                                                             nodeRoutesRegistrar.Object);
        }

        [Fact]
        public void IfDomainIsNotAllowed_SelfRegistrationIsNotSent()
        {
            var domain = Guid.NewGuid().ToString();
            var message = Message.Create(new RequestClusterMessageRoutesMessage()).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, null);
            //
            nodeRoutesRegistrar.Verify(m => m.RegisterOwnGlobalRoutes(message.Domain), Times.Never);
        }

        [Fact]
        public void IfDomainIsAllowed_RegisterOwnGlobalRoutesIsCalled()
        {
            var message = Message.Create(new RequestClusterMessageRoutesMessage()).As<Message>();
            message.SetDomain(domain);
            //
            handler.Handle(message, null);
            //
            nodeRoutesRegistrar.Verify(m => m.RegisterOwnGlobalRoutes(message.Domain), Times.Once);
        }
    }
}