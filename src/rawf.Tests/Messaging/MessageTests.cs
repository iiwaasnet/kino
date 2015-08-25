using System;
using System.Linq;
using NUnit.Framework;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Tests.Backend.Setup;

namespace rawf.Tests.Messaging
{
    [TestFixture]
    public class MessageTests
    {
        [Test]
        public void TestFlowStartMessage_HasCorrelationIdSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            Assert.IsNotNull(message.CorrelationId);
            CollectionAssert.IsNotEmpty(message.CorrelationId);
        }

        [Test]
        public void TestMessage_HasVersionSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            Assert.IsNotNull(message.Version);
            CollectionAssert.AreEqual(Message.CurrentVersion, message.Version);
        }

        [Test]
        public void TestMessage_HasIdentitySet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            Assert.IsNotNull(message.Identity);
            CollectionAssert.AreEqual(SimpleMessage.MessageIdentity, message.Identity);
        }

        [Test]
        public void TestMessage_HasDistributionPatternSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            CollectionAssert.Contains(Enum.GetValues(typeof (DistributionPattern)), message.Distribution);
        }

        [Test]
        public void TestPushRouterAddress_AddsOneMessageHop()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var socketEnpoints = new[]
                                 {
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray()),
                                     new SocketEndpoint(new Uri("tcp://localhost:40"), Guid.NewGuid().ToByteArray())
                                 };
            foreach (var socketEndpoint in socketEnpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }

            CollectionAssert.AreEqual(socketEnpoints, message.GetMessageHops());
        }

        [Test]
        public void TestMessageHops_AreConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
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

            CollectionAssert.AreEqual(socketEnpoints, message.GetMessageHops());
        }

        [Test]
        public void TestCorrelationId_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
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
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Message = messageText}, SimpleMessage.MessageIdentity);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(messageText, message.GetPayload<SimpleMessage>().Message);
            CollectionAssert.AreEqual(SimpleMessage.MessageIdentity, message.Identity);
            CollectionAssert.AreEqual(Message.CurrentVersion, message.Version);
        }


        [Test]
        public void TestRegisteringCallbackPoint_SetsCallbackIdentityAndCallbackReceiverIdentity()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackIdentity = Guid.NewGuid().ToByteArray();
            message.RegisterCallbackPoint(callbackIdentity, callbackReceiverIdentity);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.AreEqual(callbackIdentity, message.CallbackIdentity);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            CollectionAssert.IsEmpty(message.ReceiverIdentity);
        }


        [Test]
        public void TestIfCallbackIdentityIsEqualToMessageIdentity_ReceiverIdentityIsSetToCallbackReceiverIdentity()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackIdentity = SimpleMessage.MessageIdentity;
            message.RegisterCallbackPoint(callbackIdentity, callbackReceiverIdentity);

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            CollectionAssert.AreEqual(callbackIdentity, message.CallbackIdentity);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            CollectionAssert.AreEqual(callbackReceiverIdentity, message.ReceiverIdentity);
        }

        [Test]
        public void TestMessageDistribution_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            var distribution = message.Distribution;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(distribution, message.Distribution);
        }

        [Test]
        public void TestMessageTTL_IsConsistentlyTransferredViaMultipartMessage()
        {
            var random = new Random((int)(0x0000ffff & DateTime.UtcNow.Ticks));
            var ttl = TimeSpan.FromSeconds(random.Next(2, 60));
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage(), SimpleMessage.MessageIdentity);
            message.TTL = ttl;

            var multipart = new MultipartMessage(message);
            message = new Message(multipart);

            Assert.AreEqual(ttl, message.TTL);
        }
    }
}