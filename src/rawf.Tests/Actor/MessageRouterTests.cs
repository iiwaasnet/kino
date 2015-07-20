using System;
using System.Threading;
using Moq;
using NUnit.Framework;
using rawf.Actors;
using rawf.Connectivity;
using rawf.Messaging;
using rawf.Tests.Actor.Setup;

namespace rawf.Tests.Actor
{
    [TestFixture]
    public class MessageRouterTests
    {
        private static readonly TimeSpan AsyncOp = TimeSpan.FromMilliseconds(50);

        [Test]
        public void TestRegisterMessageHandlers_AddsActorIdentifier()
        {
            var connectivityProvider = new Mock<IConnectivityProvider>();
            var socket = new StubSocket();
            connectivityProvider.Setup(m => m.CreateRouterSocket()).Returns(socket);
            connectivityProvider.Setup(m => m.CreateBackendScaleOutSocket()).Returns(new StubSocket());
            connectivityProvider.Setup(m => m.CreateFrontendScaleOutSocket()).Returns(new StubSocket());

            var messageHandlerStack = new MessageHandlerStack();
            var router = new MessageRouter(connectivityProvider.Object, messageHandlerStack);
            router.Start();

            var messageIdentity = Guid.NewGuid().ToByteArray();
            var version = Guid.NewGuid().ToByteArray();
            var socketIdentity = Guid.NewGuid().ToByteArray();
            var message = Message.Create(new RegisterMessageHandlers
                                         {
                                             SocketIdentity = socketIdentity,
                                             Registrations = new[]
                                                             {
                                                                 new MessageHandlerRegistration
                                                                 {
                                                                     Identity = messageIdentity,
                                                                     Version = version,
                                                                     IdentityType = IdentityType.Actor
                                                                 }
                                                             }
                                         }, RegisterMessageHandlers.MessageIdentity);
            socket.DeliverMessage(message);

            Thread.Sleep(AsyncOp);

            var identifier = messageHandlerStack.Pop(new ActorIdentifier(version, messageIdentity));

            Assert.IsNotNull(identifier);
            Assert.IsTrue(identifier.Equals(new SocketIdentifier(socketIdentity)));
        }
    }
}