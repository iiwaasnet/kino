using kino.Connectivity;
using kino.Messaging;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class LocalSocketFactoryTests
    {
        [Test]
        public void LocalSocketFactory_AlwaysCreatesNewSocket()
        {
            var socketFactory = new LocalSocketFactory();
            var socket = socketFactory.Create<IMessage>();
            //
            Assert.AreNotEqual(socket, socketFactory.Create<IMessage>());
        }
    }
}