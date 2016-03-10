using System;
using System.Linq;
using kino.Core.Connectivity;
using kino.Core.Messaging;
using kino.Tests.Actors.Setup;
using NUnit.Framework;

namespace kino.Tests.Messaging
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void FlowStartMessage_HasCorrelationIdSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.IsNotNull(message.CorrelationId);
            CollectionAssert.IsNotEmpty(message.CorrelationId);
        }

        [Test]
        public void Message_HasVersionSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.IsNotNull(message.Version);
            CollectionAssert.AreEqual(Message.CurrentVersion, message.Version);
        }

        [Test]
        public void Message_HasIdentitySet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.IsNotNull(message.Identity);
            Assert.IsTrue(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void Message_HasDistributionPatternSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            CollectionAssert.Contains(Enum.GetValues(typeof (DistributionPattern)), message.Distribution);
        }

        [Test]
        public void Message_HasInitialyZeroHops()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.AreEqual(0, message.Hops);
        }

        [Test]
        public void AddHop_IncreasesHopsCountByOne()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            var hopsCount = 0;
            Assert.AreEqual(hopsCount, message.Hops);

            ((Message) message).AddHop();
            Assert.AreEqual(++hopsCount, message.Hops);

            ((Message)message).AddHop();
            Assert.AreEqual(++hopsCount, message.Hops);
        }

        [Test]
        public void PushRouterAddress_AddsOneRouterAddress()
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

            CollectionAssert.AreEquivalent(socketEnpoints, message.GetMessageRouting());
        }

        [Test]
        [TestCase(MessageTraceOptions.Routing, true)]
        [TestCase(MessageTraceOptions.None, false)]
        public void RouterAddressAddedToMessageHops_OnlyIfRouteTracingIsEnabled(MessageTraceOptions traceOptions, bool hopsAdded)
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
                CollectionAssert.AreEquivalent(socketEnpoints, message.GetMessageRouting());
            }
            else
            {
                CollectionAssert.AreEqual(Enumerable.Empty<SocketEndpoint>(), message.GetMessageRouting());
            }
        }

        [Test]
        public void MessageRouting_IsConsistentlyTransferredViaMultipartMessage()
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

            CollectionAssert.AreEquivalent(socketEnpoints, message.GetMessageRouting());
        }

        [Test]
        public void CorrelationId_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var correlationId = Guid.NewGuid().ToByteArray();
            message.SetCorrelationId(correlationId);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.AreEqual(correlationId, message.CorrelationId);
        }

        [Test]
        public void MessageContent_IsConsistentlyTransferredViaMultipartMessage()
        {
            var messageText = Guid.NewGuid().ToString();
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = messageText});

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(messageText, message.GetPayload<SimpleMessage>().Content);
            Assert.IsTrue(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void RegisteringCallbackPoint_SetsCallbackIdentityAndCallbackReceiverIdentity()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackMessageIdentifiers = new MessageIdentifier(Guid.NewGuid().ToByteArray(), Guid.NewGuid().ToByteArray());
            message.RegisterCallbackPoint(callbackReceiverIdentity, callbackMessageIdentifiers);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.Contains(message.CallbackPoint, callbackMessageIdentifiers);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            CollectionAssert.IsEmpty(message.ReceiverIdentity);
        }

        [Test]
        public void IfCallbackIdentityIsEqualToMessageIdentity_ReceiverIdentityIsSetToCallbackReceiverIdentity()
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
        public void MessageDistribution_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var distribution = message.Distribution;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(distribution, message.Distribution);
        }

        [Test]
        public void MessageHops_AreConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.AddHop();
            message.AddHop();
            var hops = message.Hops;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(hops, message.Hops);
        }


        [Test]
        public void MessageWireFormatVersion_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            Assert.AreEqual(1, message.WireFormatVersion);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(1, message.WireFormatVersion);
        }        

        [Test]
        [TestCase(MessageTraceOptions.None)]
        [TestCase(MessageTraceOptions.Routing)]
        public void MessageTraceOptions_IsConsistentlyTransferredViaMultipartMessage(MessageTraceOptions routeOptions)
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = routeOptions;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(routeOptions, message.TraceOptions);
        }

        [Test]
        public void MessageTTL_IsConsistentlyTransferredViaMultipartMessage()
        {
            var random = new Random((int) (0x0000ffff & DateTime.UtcNow.Ticks));
            var ttl = TimeSpan.FromSeconds(random.Next(2, 60));
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TTL = ttl;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(ttl, message.TTL);
        }

        [Test]
        public void CallbackTriggeresForEveryMessageInCallbackPoint()
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