using kino.Client;
using kino.Diagnostics;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using Moq;
using NUnit.Framework;

namespace kino.Tests.Client
{
    [TestFixture]
    public class MessageTracerTests
    {
        private IMessage message;
        private Mock<ILogger> logger;

        [SetUp]
        public void Setup()
        {
            message = Message.Create(new SimpleMessage(), SimpleMessage.MessageIdentity);
            logger = new Mock<ILogger>();
        }

        [Test]
        public void TestIfMessageHasRoutingTraceOption_LoggerTraceMethodIsCalled()
        {
            message.TraceOptions = MessageTraceOptions.Routing;
            var messageTracer = new MessageTracer(logger.Object);
            messageTracer.CallbackNotFound(message);

            logger.Verify(m => m.Trace(It.IsAny<object>()), Times.Once);
        }

        [Test]
        public void TestIfMessagDoesntHaveRoutingTraceOption_LoggerTraceMethodIsNotCalled()
        {
            var messageTracer = new MessageTracer(logger.Object);
            messageTracer.CallbackNotFound(message);

            logger.Verify(m => m.Trace(It.IsAny<object>()), Times.Never);
        }
    }
}