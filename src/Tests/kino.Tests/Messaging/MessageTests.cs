using System;
using System.Linq;
using System.Security.Cryptography;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Security;
using kino.Tests.Actors.Setup;
using kino.Tests.Helpers;
using Xunit;
using CollectionAssert = FluentAssertions.AssertionExtensions;

namespace kino.Tests.Messaging
{
    public class MessageTests
    {
        private readonly ISecurityProvider securityProvider;

        public MessageTests()
            => securityProvider = new SecurityProvider(HMACMD5.Create, new DomainScopeResolver(), new DomainPrivateKeyProvider());

        [Fact]
        public void FlowStartMessage_HasCorrelationIdSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.NotNull(message.CorrelationId);
            Assert.NotEmpty(message.CorrelationId);
        }

        [Fact]
        public void Message_HasVersionSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.NotNull(message.Version);
            Assert.Equal(Message.CurrentVersion, message.Version);
        }

        [Fact]
        public void Message_HasIdentitySet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.NotNull(message.Identity);
            Assert.True(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Fact]
        public void Message_HasDistributionPatternSet()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.Contains(message.Distribution, Enum.GetValues(typeof(DistributionPattern)).OfType<DistributionPattern>());
        }

        [Fact]
        public void Message_HasInitialyZeroHops()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());

            Assert.Equal(0, message.Hops);
        }

        [Fact]
        public void AddHop_IncreasesHopsCountByOne()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage());
            var hopsCount = 0;
            Assert.Equal(hopsCount, message.Hops);

            ((Message) message).AddHop();
            Assert.Equal(++hopsCount, message.Hops);

            ((Message) message).AddHop();
            Assert.Equal(++hopsCount, message.Hops);
        }

        [Fact]
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

            CollectionAssert.Should(socketEnpoints).BeEquivalentTo(message.GetMessageRouting());
        }

        [Theory]
        [InlineData(MessageTraceOptions.Routing)]
        [InlineData(MessageTraceOptions.None)]
        public void RouterAddress_AlwaysAddedToMessageHops(MessageTraceOptions traceOptions)
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
            CollectionAssert.Should(socketEnpoints).BeEquivalentTo(message.GetMessageRouting());
        }

        [Fact]
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
            message = Message.FromMultipartMessage(multipart);

            CollectionAssert.Should(socketEnpoints).BeEquivalentTo(message.GetMessageRouting());
        }

        [Fact]
        public void CorrelationId_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            var correlationId = Guid.NewGuid().ToByteArray();
            message.SetCorrelationId(correlationId);

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(correlationId, message.CorrelationId);
        }

        [Fact]
        public void ReceiverNode_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var receiverNode = ReceiverIdentifier.Create();
            message.SetReceiverNode(receiverNode);

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(receiverNode.Identity, message.ReceiverNodeIdentity);
        }

        [Fact]
        public void MessageContent_IsConsistentlyTransferredViaMultipartMessage()
        {
            var messageText = Guid.NewGuid().ToString();
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage {Content = messageText});

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(messageText, message.GetPayload<SimpleMessage>().Content);
            Assert.True(message.Equals(MessageIdentifier.Create<SimpleMessage>()));
        }

        [Fact]
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

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Contains(callbackMessageIdentifier, message.CallbackPoint);
            Assert.Equal(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            Assert.Equal(callbackReceiverNodeIdentity, message.CallbackReceiverNodeIdentity);
            Assert.Empty(message.ReceiverIdentity);
        }

        [Fact]
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

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Contains(callbackMessageIdentifier, message.CallbackPoint);
            Assert.Equal(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            Assert.Equal(callbackReceiverIdentity, message.ReceiverIdentity);
            Assert.Equal(callbackReceiverNodeIdentity, message.CallbackReceiverNodeIdentity);
            Assert.Equal(callbackReceiverNodeIdentity, message.ReceiverNodeIdentity);
        }

        [Theory]
        [InlineData(DistributionPattern.Broadcast)]
        [InlineData(DistributionPattern.Unicast)]
        public void MessageDistribution_IsConsistentlyTransferredViaMultipartMessage(DistributionPattern distributionPattern)
        {
            var message = Message.Create(new SimpleMessage(), distributionPattern).As<Message>();

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(distributionPattern, message.Distribution);
        }

        [Fact]
        public void MessagePartition_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage
                                                                   {
                                                                       Partition = Guid.NewGuid().ToByteArray()
                                                                   });
            var partition = message.Partition;

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.True(Unsafe.ArraysEqual(partition, message.Partition));
        }

        [Fact]
        public void MessageHops_AreConsistentlyTransferredViaMultipartMessage()
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.AddHop();
            message.AddHop();
            var hops = message.Hops;

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(hops, message.Hops);
        }

        [Fact]
        public void MessageWireFormatVersion_IsConsistentlyTransferredViaMultipartMessage()
        {
            const int wireMessageFormat = 5;

            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            Assert.Equal(wireMessageFormat, message.WireFormatVersion);

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(wireMessageFormat, message.WireFormatVersion);
        }

        [Theory]
        [InlineData(MessageTraceOptions.None)]
        [InlineData(MessageTraceOptions.Routing)]
        public void MessageTraceOptions_IsConsistentlyTransferredViaMultipartMessage(MessageTraceOptions routeOptions)
        {
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TraceOptions = routeOptions;

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(routeOptions, message.TraceOptions);
        }

        [Fact]
        public void MessageTTL_IsConsistentlyTransferredViaMultipartMessage()
        {
            var ttl = TimeSpan.FromSeconds(Randomizer.Int32(2, 60));
            var message = (Message) Message.CreateFlowStartMessage(new SimpleMessage());
            message.TTL = ttl;

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(ttl, message.TTL);
        }

        [Fact]
        public void SecurityDomain_IsConsistentlyTransferredViaMultipartMessage()
        {
            var securityDomain = Guid.NewGuid().ToString();
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            message.SetDomain(securityDomain);

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(securityDomain, message.Domain);
        }

        [Fact]
        public void CallbackKey_IsConsistentlyTransferredViaMultipartMessage()
        {
            var message = Message.CreateFlowStartMessage(new SimpleMessage()).As<Message>();
            var callbackKey = Randomizer.Int32(1, Int32.MaxValue);
            var callbackMessageIdentifier = MessageIdentifier.Create<SimpleMessage>();
            message.RegisterCallbackPoint(Guid.NewGuid().ToByteArray(),
                                          Guid.NewGuid().ToByteArray(),
                                          callbackMessageIdentifier,
                                          callbackKey);
            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            Assert.Equal(callbackKey, message.CallbackKey);
        }

        [Fact]
        public void MessageSignature_IsConsistentlyTransferredViaMultipartMessage()
        {
            var simpleMessage = new SimpleMessage();
            var securityDomain = securityProvider.GetDomain(simpleMessage.Identity);
            var message = Message.CreateFlowStartMessage(simpleMessage).As<Message>();
            message.SetDomain(securityDomain);
            message.SignMessage(securityProvider);

            var multipart = new MultipartMessage(message);
            message = Message.FromMultipartMessage(multipart);

            message.VerifySignature(securityProvider);
        }

        [Fact]
        public void CallbackTriggeresForEveryMessageInCallbackPoint()
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
                Assert.Equal(callbackReceiverIdentity, message.CallbackReceiverIdentity);
            }
        }
    }
}