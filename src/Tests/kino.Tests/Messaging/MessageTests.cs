using System;
using System.Linq;
using kino.Connectivity;
using kino.Messaging;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Messaging
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void TestFlowStartMessage_HasCorrelationIdSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.IsNotNull(message.CorrelationId);
            CollectionAssert.IsNotEmpty(message.CorrelationId);
        }

        [Test]
        public void TestMessage_HasVersionSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.IsNotNull(message.Version);
            CollectionAssert.AreEqual(Message.CurrentVersion, message.Version);
        }

        [Test]
        public void TestMessage_HasIdentitySet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.IsNotNull(message.Identity);
            CollectionAssert.AreEqual(MessageIdentifier.Create<SimpleMessage>().Identity, message.Identity);
        }

        [Test]
        public void TestMessage_HasDistributionPatternSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            CollectionAssert.Contains(Enum.GetValues(typeof (DistributionPattern)), message.Distribution);
        }

        [Test]
        public void TestPushRouterAddress_AddsOneMessageHop()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = MessageTraceOptions.Routing;
            var socketEnpoints = new[]
                                 {
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray()),
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray())
                                 };
            foreach (var socketEndpoint in socketEnpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }

            CollectionAssert.AreEquivalent(socketEnpoints, message.GetMessageHops());
        }

        [Test]
        [TestCase(MessageTraceOptions.Routing, true)]
        [TestCase(MessageTraceOptions.None, false)]
        public void TestRouterAddressAddeToMessageHops_OnlyIfRouteTracingIsEnabled(MessageTraceOptions traceOptions, bool hopsAdded)
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = traceOptions;
            var socketEnpoints = new[]
                                 {
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray()),
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray())
                                 };
            foreach (var socketEndpoint in socketEnpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }
            if (hopsAdded)
            {
                CollectionAssert.AreEquivalent(socketEnpoints, message.GetMessageHops());
            }
            else
            {
                CollectionAssert.AreEqual(Enumerable.Empty<SocketEndpoint>(), message.GetMessageHops());
            }
        }

        [Test]
        public void TestMessageHops_AreConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = MessageTraceOptions.Routing;
            var socketEnpoints = new[]
                                 {
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray()),
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray())
                                 };
            foreach (var socketEndpoint in socketEnpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.AreEquivalent(socketEnpoints, message.GetMessageHops());
        }

        [Test]
        public void TestCorrelationId_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var correlationId = Guid.NewGuid().ToByteArray();
            message.SetCorrelationId(correlationId);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.AreEqual(correlationId, message.CorrelationId);
        }

        [Test]
        public void TestMessageContent_IsConsistentlyTransferredViaMultipartMessage()
        {
            var messageText = Guid.NewGuid().ToString();
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = messageText});

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(messageText, message.GetPayload<SimpleMessage>().Content);
            CollectionAssert.AreEqual(MessageIdentifier.Create<SimpleMessage>().Identity, message.Identity);
            CollectionAssert.AreEqual(Message.CurrentVersion, message.Version);
        }


        [Test]
        public void TestRegisteringCallbackPoint_SetsCallbackIdentityAndCallbackReceiverIdentity()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackMessageIdentifiers = new MessageIdentifier(Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray());
            message.RegisterCallbackPoint( callbackReceiverIdentity, callbackMessageIdentifiers);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.Contains(message.CallbackPoint, callbackMessageIdentifiers);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            CollectionAssert.IsEmpty(message.ReceiverIdentity);
        }


        [Test]
        public void TestIfCallbackIdentityIsEqualToMessageIdentity_ReceiverIdentityIsSetToCallbackReceiverIdentity()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackIdentifier = MessageIdentifier.Create<SimpleMessage>();
            message.RegisterCallbackPoint(callbackReceiverIdentity, callbackIdentifier);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.Contains(message.CallbackPoint, callbackIdentifier);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.ReceiverIdentity);
        }

        [Test]
        public void TestMessageDistribution_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var distribution = message.Distribution;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(distribution, message.Distribution);
        }

        [Test]
        [TestCase(MessageTraceOptions.None)]
        [TestCase(MessageTraceOptions.Routing)]
        public void TestMessageTraceOptions_IsConsistentlyTransferredViaMultipartMessage(MessageTraceOptions routeOptions)
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = routeOptions;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(routeOptions, message.TraceOptions);
        }

        [Test]
        public void TestMessageTTL_IsConsistentlyTransferredViaMultipartMessage()
        {
            var random = new Random((int)(0x0000ffff & DateTime.UtcNow.Ticks));
            var ttl = TimeSpan.FromSeconds(random.Next(2, 60));
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TTL = ttl;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(ttl, message.TTL);
        }

        [Test]
        public void TestCallbackTriggeresForEveryMessageInCallbackPoint()
        {
            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackMessageIdentifier = new[]
                                            {
                                                MessageIdentifier.Create<SimpleMessage>(), 
                                                MessageIdentifier.Create<AsyncExceptionMessage>(), 
                                                MessageIdentifier.Create<AsyncMessage>()
                                            };
            var messages = new[]
                           {
                               Message.Create(new SimpleMessage()),
                               Message.Create(new AsyncExceptionMessage()),
                               Message.Create(new AsyncMessage()),
                           };

            foreach (Message message in messages)
            {
                message.RegisterCallbackPoint(callbackReceiverIdentity, callbackMessageIdentifier);
                CollectionAssert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            }
        }
    }
}