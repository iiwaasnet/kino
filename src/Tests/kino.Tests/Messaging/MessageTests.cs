using System;
using System.Linq;
using System.Security.Cryptography;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using NUnit.Framework;

namespace kino.Tests.Messaging
{
    public class MessageTests
    {
        private ISecurityProvider securityProvider;
        private MessageWireFormatter messageWireFormatter;

        [SetUp]
        public void Setup()
        {
            messageWireFormatter = new MessageWireFormatter();
            securityProvider = new SecurityProvider(() => HMACMD5.Create("HMACSHA256"),
                                                    new DomainScopeResolver(),
                                                    new DomainPrivateKeyProvider());
        }

        [Test]
        public void FlowStartMessage_HasCorrelationIdSet()
        {
            // act
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            // assert
            Assert.NotNull(message.CorrelationId);
            CollectionAssert.IsNotEmpty(message.CorrelationId);
        }

        [Test]
        public void Message_HasVersionSet()
        {
            // act
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            // assert
            Assert.NotNull(message.Version);
            Assert.AreEqual(Message.CurrentVersion, message.Version);
        }

        [Test]
        public void Message_HasIdentitySet()
        {
            // act
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            // assert
            Assert.NotNull(message.Identity);
            Assert.True(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void Message_HasDistributionPatternSet()
        {
            // act
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            // assert
            CollectionAssert.Contains(Enum.GetValues(typeof(DistributionPattern)).OfType<DistributionPattern>(), message.Distribution);
        }

        [Test]
        public void Message_HasInitiallyZeroHops()
        {
            // act
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            // assert
            Assert.AreEqual(0, message.Hops);
        }

        [Test]
        public void AddHop_IncreasesHopsCountByOne()
        {
            // arrange
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            var hopsCount = 0;
            // act
            Assert.AreEqual(hopsCount, message.Hops);
            // act
            ((Message) message).AddHop();
            Assert.AreEqual(++hopsCount, message.Hops);
            // act
            ((Message) message).AddHop();
            Assert.AreEqual(++hopsCount, message.Hops);
        }

        [Test]
        public void PushRouterAddress_AddsOneRouterAddress()
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = MessageTraceOptions.Routing;
            var socketEndpoints = new[]
                                  {
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray()),
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray())
                                  };
            // act
            foreach (var socketEndpoint in socketEndpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }
            // assert
            CollectionAssert.AreEquivalent(socketEndpoints, message.GetMessageRouting());
        }

        [Test]
        [TestCase(MessageTraceOptions.Routing)]
        [TestCase(MessageTraceOptions.None)]
        public void RouterAddress_AlwaysAddedToMessageHops(MessageTraceOptions traceOptions)
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = traceOptions;
            var socketEndpoints = new[]
                                  {
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray()),
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray())
                                  };
            // act
            foreach (var socketEndpoint in socketEndpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }
            // assert
            CollectionAssert.AreEquivalent(socketEndpoints, message.GetMessageRouting());
        }

        [Test]
        public void MessageRouting_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = MessageTraceOptions.Routing;
            var socketEndpoints = new[]
                                  {
                                      SocketEndpoint.Parse("tcp://localhost:41", Guid.NewGuid().ToByteArray()),
                                      SocketEndpoint.Parse("tcp://localhost:42", Guid.NewGuid().ToByteArray())
                                  };
            foreach (var socketEndpoint in socketEndpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            var messageRouting = message.GetMessageRouting();
            CollectionAssert.AreEquivalent(socketEndpoints, messageRouting);
        }

        [Test]
        public void CorrelationId_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var correlationId = Guid.NewGuid().ToByteArray();
            message.SetCorrelationId(correlationId);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(correlationId, message.CorrelationId);
        }

        [Test]
        public void ReceiverNode_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var receiverNode = ReceiverIdentifier.Create();
            message.SetReceiverNode(receiverNode);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(receiverNode.Identity, message.ReceiverNodeIdentity);
        }

        [Test]
        public void MessageContent_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var messageText = Guid.NewGuid().ToString();
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = messageText});
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(messageText, message.GetPayload<SimpleMessage>().Content);
            Assert.True(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void RegisteringCallbackPoint_SetsCallbackIdentityAndCallbackReceiverIdentity()
        {
            // arrange
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackReceiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var callbackMessageIdentifier = new MessageIdentifier(Guid.NewGuid().ToByteArray(),
                                                                  Randomizer.UInt16(),
                                                                  Guid.NewGuid().ToByteArray());
            var callbackKey = Randomizer.Int32();
            message.RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                          callbackReceiverIdentity,
                                          callbackMessageIdentifier,
                                          callbackKey);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            CollectionAssert.Contains(message.CallbackPoint, callbackMessageIdentifier);
            Assert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            Assert.AreEqual(callbackReceiverNodeIdentity, message.CallbackReceiverNodeIdentity);
            CollectionAssert.IsEmpty(message.ReceiverIdentity);
        }

        [Test]
        public void IfCallbackIdentityIsEqualToMessageIdentity_ReceiverIdentitiesAreSetToCallbackReceiverIdentities()
        {
            // arrange
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackReceiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var callbackMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var callbackKey = Randomizer.Int32();

            message.RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                          callbackReceiverIdentity,
                                          callbackMessageIdentifier,
                                          callbackKey);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            CollectionAssert.Contains(message.CallbackPoint, callbackMessageIdentifier);
            Assert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            Assert.AreEqual(callbackReceiverIdentity, message.ReceiverIdentity);
            Assert.AreEqual(callbackReceiverNodeIdentity, message.CallbackReceiverNodeIdentity);
            Assert.AreEqual(callbackReceiverNodeIdentity, message.ReceiverNodeIdentity);
        }

        [Test]
        [TestCase(DistributionPattern.Broadcast)]
        [TestCase(DistributionPattern.Unicast)]
        public void MessageDistribution_IsConsistentlyTransferredOverWires(DistributionPattern distributionPattern)
        {
            // arrange
            var message = Message.Create(new SimpleMessage(), distributionPattern).As<Message>();
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(distributionPattern, message.Distribution);
        }

        [Test]
        public void MessagePartition_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage
                                                                   {
                                                                       Partition = Guid.NewGuid().ToByteArray()
                                                                   });
            var partition = message.Partition;
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.True(Unsafe.ArraysEqual(partition, message.Partition));
        }

        [Test]
        public void MessageHops_AreConsistentlyTransferredOverWires()
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.AddHop();
            message.AddHop();
            var hops = message.Hops;
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(hops, message.Hops);
        }

        [Test]
        public void MessageWireFormatVersion_IsConsistentlyTransferredOverWires()
        {
            // arrange
            const int wireMessageFormat = 5;

            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            Assert.AreEqual(wireMessageFormat, message.WireFormatVersion);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(wireMessageFormat, message.WireFormatVersion);
        }

        [Test]
        [TestCase(MessageTraceOptions.None)]
        [TestCase(MessageTraceOptions.Routing)]
        public void MessageTraceOptions_IsConsistentlyTransferredOverWires(MessageTraceOptions routeOptions)
        {
            // arrange
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = routeOptions;
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(routeOptions, message.TraceOptions);
        }

        [Test]
        public void MessageTTL_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var ttl = TimeSpan.FromSeconds(Randomizer.Int32(2, 60));
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TTL = ttl;
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(ttl, message.TTL);
        }

        [Test]
        public void SecurityDomain_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var securityDomain = Guid.NewGuid().ToString();
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            message.SetDomain(securityDomain);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(securityDomain, message.Domain);
        }

        [Test]
        public void CallbackKey_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var callbackKey = Randomizer.Int32(1, Int32.MaxValue);
            var callbackMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            message.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                          Guid.NewGuid().ToByteArray(),
                                          callbackMessageIdentifier,
                                          callbackKey);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            Assert.AreEqual(callbackKey, message.CallbackKey);
        }

        [Test]
        public void MessageSignature_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var simpleMessage = new SimpleMessage();
            var securityDomain = securityProvider.GetDomain(simpleMessage.Identity);
            var message = Message.CreateFlowStartMessage(simpleMessage).As<Message>();
            message.SetDomain(securityDomain);
            message.SignMessage(securityProvider);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            message.VerifySignature(securityProvider);
        }

        [Test]
        public void MessagePayload_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var simpleMessage = new SimpleMessage
                                {
                                    Content = Guid.NewGuid().ToString()
                                };
            var securityDomain = securityProvider.GetDomain(simpleMessage.Identity);
            var message = Message.CreateFlowStartMessage(simpleMessage).As<Message>();
            message.SetDomain(securityDomain);
            message.SignMessage(securityProvider);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            InsertNewSocketIdFrame();
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            var payload = message.GetPayload<SimpleMessage>();
            Assert.AreEqual(simpleMessage.Content, payload.Content);

            void InsertNewSocketIdFrame()
                => wireFrames.Insert(0, Guid.NewGuid().ToByteArray());
        }

        [Test]
        public void CallbackTriggersForEveryMessageInCallbackPoint()
        {
            // arrange
            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackReceiverNodeIdentity = Guid.NewGuid().ToByteArray();
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

            foreach (var message in messages.OfType<Message>())
            {
                // act
                message.RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                              callbackReceiverIdentity,
                                              callbackMessageIdentifier,
                                              Randomizer.Int32());
                // assert
                Assert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            }
        }
    }
}