using System;
using kino.Core.Connectivity;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class SocketEndpointTests
    {
        [Test]
        public void TestTwoSocketEndpointsAreEqual_IfUriAndIdentityAreEqual()
        {
            var localhost = "tcp://127.0.0.1:4000";
            var identity = Guid.NewGuid().ToByteArray();
            var ep1 = new SocketEndpoint(new Uri(localhost), identity);
            var ep2 = new SocketEndpoint(new Uri(localhost), identity);

            Assert.AreEqual(ep1, ep2);

            var ep3 = new SocketEndpoint(new Uri(localhost), Guid.NewGuid().ToByteArray());

            Assert.AreNotEqual(ep1, ep3);
        }
    }
}