using System;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Core.Framework;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Connectivity
{
    [TestFixture]
    public class LocalSocketTests
    {
        private ILocalSocket<IMessage> socket;

        [SetUp]
        public void Setup()
        {
            socket = new LocalSocket<IMessage>();
        }

        [Test]
        public void CanReceiveBlocks_IfMessageIsNotSentToSocket()
        {
            Assert.IsFalse(socket.CanReceive().WaitOne(TimeSpan.FromSeconds(3)));
        }

        [Test]
        public void CanReceiveUnblocks_WhenMessageIsSentToSocket()
        {
            var asyncOp = TimeSpan.FromSeconds(4);
            Task.Factory.StartNew(() =>
                                  {
                                      asyncOp.DivideBy(2).Sleep();
                                      socket.Send(Message.Create(new SimpleMessage()));
                                  });
            //
            Assert.IsTrue(socket.CanReceive().WaitOne(asyncOp));
        }

        [Test]
        public void TryReceive_ReturnsNullIfMessageIsNotSentToSocket()
        {
            Assert.IsNull(socket.TryReceive());
        }

        [Test]
        public void TryReceiver_ReturnsAllMessagesSentToSocket()
        {
            var message1 = Message.Create(new SimpleMessage());
            var message2 = Message.Create(new NullMessage());
            socket.Send(message1);
            socket.Send(message2);
            //
            Assert.AreEqual(message1, socket.TryReceive());
            Assert.AreEqual(message2, socket.TryReceive());
        }
    }
}