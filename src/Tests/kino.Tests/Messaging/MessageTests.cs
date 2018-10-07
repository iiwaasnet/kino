using System;
using System.Linq;
using System.Security.Cryptography;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
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
            securityProvider = new SecurityProvider(() => HMACMD5.Create("HMACSHA256"), new DomainScopeResolver(), new DomainPrivateKeyProvider());
        }

        [Test]
        public void FlowStartMessage_HasCorrelationIdSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.NotNull(message.CorrelationId);
            CollectionAssert.IsNotEmpty(message.CorrelationId);
        }

        [Test]
        public void Message_HasVersionSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.NotNull(message.Version);
            Assert.AreEqual(Message.CurrentVersion, message.Version);
        }

        [Test]
        public void Message_HasIdentitySet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.NotNull(message.Identity);
            Assert.True(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void Message_HasDistributionPatternSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            CollectionAssert.Contains(Enum.GetValues(typeof(DistributionPattern)).OfType<DistributionPattern>(), message.Distribution);
        }

        [Test]
        public void Message_HasInitiallyZeroHops()
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

            ((Message) message).AddHop();
            Assert.AreEqual(++hopsCount, message.Hops);
        }

        [Test]
        public void PushRouterAddress_AddsOneRouterAddress()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = MessageTraceOptions.Routing;
            var socketEndpoints = new[]
                                  {
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray()),
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray())
                                  };
            foreach (var socketEndpoint in socketEndpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }

            CollectionAssert.AreEquivalent(socketEndpoints, message.GetMessageRouting());
        }

        [Test]
        [TestCase(MessageTraceOptions.Routing)]
        [TestCase(MessageTraceOptions.None)]
        public void RouterAddress_AlwaysAddedToMessageHops(MessageTraceOptions traceOptions)
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = traceOptions;
            var socketEndpoints = new[]
                                  {
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray()),
                                      SocketEndpoint.Parse("tcp://localhost:40", Guid.NewGuid().ToByteArray())
                                  };
            foreach (var socketEndpoint in socketEndpoints)
            {
                message.PushRouterAddress(socketEndpoint);
            }
            CollectionAssert.AreEquivalent(socketEndpoints, message.GetMessageRouting());
        }

        [Test]
        public void MessageRouting_IsConsistentlyTransferredOverWires()
        {
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

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            var messageRouting = message.GetMessageRouting();
            CollectionAssert.AreEquivalent(socketEndpoints, messageRouting);
        }

        [Test]
        public void CorrelationId_IsConsistentlyTransferredOverWires()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var correlationId = Guid.NewGuid().ToByteArray();
            message.SetCorrelationId(correlationId);

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(correlationId, message.CorrelationId);
        }

        [Test]
        public void ReceiverNode_IsConsistentlyTransferredOverWires()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var receiverNode = ReceiverIdentifier.Create();
            message.SetReceiverNode(receiverNode);

            var wireFrames = messageWireFormatter.Serialize(message);
            ;
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(receiverNode.Identity, message.ReceiverNodeIdentity);
        }

        [Test]
        public void MessageContent_IsConsistentlyTransferredOverWires()
        {
            var messageText = Guid.NewGuid().ToString();
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = messageText});

            var wireFrames = messageWireFormatter.Serialize(message);
            ;
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(messageText, message.GetPayload<SimpleMessage>().Content);
            Assert.True(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Test]
        public void RegisteringCallbackPoint_SetsCallbackIdentityAndCallbackReceiverIdentity()
        {
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

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            CollectionAssert.Contains(message.CallbackPoint, callbackMessageIdentifier);
            Assert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            Assert.AreEqual(callbackReceiverNodeIdentity, message.CallbackReceiverNodeIdentity);
            CollectionAssert.IsEmpty(message.ReceiverIdentity);
        }

        [Test]
        public void IfCallbackIdentityIsEqualToMessageIdentity_ReceiverIdentitiesAreSetToCallbackReceiverIdentities()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();

            var callbackReceiverIdentity = Guid.NewGuid().ToByteArray();
            var callbackReceiverNodeIdentity = Guid.NewGuid().ToByteArray();
            var callbackMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            var callbackKey = Randomizer.Int32();

            message.RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                          callbackReceiverIdentity,
                                          callbackMessageIdentifier,
                                          callbackKey);

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

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
            var message = Message.Create(new SimpleMessage(), distributionPattern).As<Message>();

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(distributionPattern, message.Distribution);
        }

        [Test]
        public void MessagePartition_IsConsistentlyTransferredOverWires()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage
                                                                   {
                                                                       Partition = Guid.NewGuid().ToByteArray()
                                                                   });
            var partition = message.Partition;

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.True(Unsafe.ArraysEqual(partition, message.Partition));
        }

        [Test]
        public void MessageHops_AreConsistentlyTransferredOverWires()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.AddHop();
            message.AddHop();
            var hops = message.Hops;

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(hops, message.Hops);
        }

        [Test]
        public void MessageWireFormatVersion_IsConsistentlyTransferredOverWires()
        {
            const int wireMessageFormat = 5;

            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            Assert.AreEqual(wireMessageFormat, message.WireFormatVersion);

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(wireMessageFormat, message.WireFormatVersion);
        }

        [Test]
        [TestCase(MessageTraceOptions.None)]
        [TestCase(MessageTraceOptions.Routing)]
        public void MessageTraceOptions_IsConsistentlyTransferredOverWires(MessageTraceOptions routeOptions)
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = routeOptions;

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(routeOptions, message.TraceOptions);
        }

        [Test]
        public void MessageTTL_IsConsistentlyTransferredOverWires()
        {
            var ttl = TimeSpan.FromSeconds(Randomizer.Int32(2, 60));
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TTL = ttl;

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(ttl, message.TTL);
        }

        [Test]
        public void SecurityDomain_IsConsistentlyTransferredOverWires()
        {
            var securityDomain = Guid.NewGuid().ToString();
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            message.SetDomain(securityDomain);

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(securityDomain, message.Domain);
        }

        [Test]
        public void CallbackKey_IsConsistentlyTransferredOverWires()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var callbackKey = Randomizer.Int32(1, Int32.MaxValue);
            var callbackMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            message.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                          Guid.NewGuid().ToByteArray(),
                                          callbackMessageIdentifier,
                                          callbackKey);

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            Assert.AreEqual(callbackKey, message.CallbackKey);
        }

        [Test]
        public void MessageSignature_IsConsistentlyTransferredOverWires()
        {
            var simpleMessage = new SimpleMessage();
            var securityDomain = securityProvider.GetDomain(simpleMessage.Identity);
            var message = Message.CreateFlowStartMessage(simpleMessage).As<Message>();
            message.SetDomain(securityDomain);
            message.SignMessage(securityProvider);

            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();

            message.VerifySignature(securityProvider);
        }

        [Test]
        public void MessagePayload_IsConsistentlyTransferredOverWires()
        {
            // arrange
            var registrationMessage = new RegisterExternalMessageRouteMessage
                                      {
                                          Uri = Guid.NewGuid().ToString(),
                                          NodeIdentity = Guid.NewGuid().ToByteArray(),
                                          Health = new Health
                                                   {
                                                       Uri = Guid.NewGuid().ToString(),
                                                       HeartBeatInterval = TimeSpan.FromSeconds(Randomizer.Int32(1, 60))
                                                   },
                                          Routes = new[]
                                                   {
                                                       new RouteRegistration
                                                       {
                                                           ReceiverIdentity = Guid.NewGuid().ToByteArray(),
                                                           MessageContracts = new[]
                                                                              {
                                                                                  new MessageContract
                                                                                  {
                                                                                      Partition = Guid.NewGuid().ToByteArray(),
                                                                                      Identity = Guid.NewGuid().ToByteArray(),
                                                                                      Version = Randomizer.UInt16()
                                                                                  }
                                                                              }
                                                       }
                                                   }
                                      };
            var securityDomain = securityProvider.GetDomain(registrationMessage.Identity);
            var message = Message.CreateFlowStartMessage(registrationMessage).As<Message>();
            message.SetDomain(securityDomain);
            message.SignMessage(securityProvider);
            // act
            var wireFrames = messageWireFormatter.Serialize(message);
            message = messageWireFormatter.Deserialize(wireFrames).As<Message>();
            // assert
            var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();
            Assert.IsTrue(Unsafe.ArraysEqual(registrationMessage.NodeIdentity, payload.NodeIdentity));
            Assert.AreEqual(registrationMessage.Uri, payload.Uri);
        }

        [Test]
        public void CallbackTriggersForEveryMessageInCallbackPoint()
        {
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
                message.RegisterCallbackPoint(callbackReceiverNodeIdentity,
                                              callbackReceiverIdentity,
                                              callbackMessageIdentifier,
                                              Randomizer.Int32());
                Assert.AreEqual(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            }
        }
    }
}