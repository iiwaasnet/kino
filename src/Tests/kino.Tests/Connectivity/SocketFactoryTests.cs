using System;
using kino.Connectivity;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class SocketFactoryTests
    {
        [Test]
        public void IfConfigurationIsNotProvided_DefaultConfigurationIsCreated()
        {
            var socketFactory = new SocketFactory(null);
            //
            Assert.IsNotNull(socketFactory.GetSocketDefaultConfiguration());
        }

        [Test]
        public void CreateSocket_AlwaysCreatesNewSocket()
        {
            TestNewSocketCreated(f => f.CreatePublisherSocket());
            TestNewSocketCreated(f => f.CreateDealerSocket());
            TestNewSocketCreated(f => f.CreateRouterSocket());
            TestNewSocketCreated(f => f.CreateSubscriberSocket());
        }

        private void TestNewSocketCreated(Func<ISocketFactory, ISocket> func)
        {
            var socketFactory = new SocketFactory(null);
            var socket = func(socketFactory);
            Assert.AreNotEqual(socket, func(socketFactory));
        }
    }
}