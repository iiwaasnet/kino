using System;
using kino.Connectivity;
using Moq;
using NetMQ;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class SocketExtensionsTests
    {
        [Test]
        public void SafeDisconnect_DoesntThrowEndpointNotFoundException()
        {
            var socket = new Mock<ISocket>();
            socket.Setup(m => m.Disconnect(It.IsAny<Uri>())).Throws<EndpointNotFoundException>();
            //
            Assert.DoesNotThrow(() => socket.Object.SafeDisconnect(null));
        }
    }
}