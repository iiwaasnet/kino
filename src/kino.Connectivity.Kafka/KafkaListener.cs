using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Confluent.Kafka;
using kino.Core;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Kafka;

namespace kino.Connectivity.Kafka
{
    public class KafkaListener : IListener
    {
        private static readonly IMessageSerializer DefaultSerializer = new ProtobufMessageSerializer();
        private readonly IConsumer<Null, byte[]> consumer;
        private readonly ReceiverIdentifier socketIdentity;

        public KafkaListener(KafkaListenerConfiguration config,
                             string groupId,
                             string topic)
        {
            socketIdentity = ReceiverIdentifier.Create();
            var consumerConfig = new ConsumerConfig
                                 {
                                     GroupId = groupId,
                                     AutoOffsetReset = config.AutoOffsetReset,
                                     BootstrapServers = config.BootstrapServers,
                                     ClientId = $"{Environment.MachineName}-{Process.GetCurrentProcess().Id}",
                                     SessionTimeoutMs = config.SessionTimeout.HasValue
                                                            ? (int?) config.SessionTimeout.Value.TotalMilliseconds
                                                            : null,
                                     MaxPollIntervalMs = config.MaxPollInterval.HasValue
                                                             ? (int?) config.MaxPollInterval.Value.TotalMilliseconds
                                                             : null,
                                     EnableAutoCommit = false,
                                     LogConnectionClose = false,
                                     SocketKeepaliveEnable = config.SocketKeepAliveEnabled,
                                     ApiVersionRequest = true
                                 };
            consumer = new ConsumerBuilder<Null, byte[]>(consumerConfig).Build();
            consumer.Subscribe(topic);
        }

        public IMessage Receive(CancellationToken token)
        {
            var res = consumer.Consume(token);
            consumer.Commit();

            ReceiveRate?.Increment();

            var wireFormatVersion = res.Headers[0].GetValueBytes().GetUShort();

            var msg = DefaultSerializer.Deserialize<WireMessage>(res.Value);

            var message = CreateMessage(msg);

            return message;
        }

        private static Message CreateMessage(WireMessage msg)
        {
            var message = new Message(msg.Identity, msg.Version, msg.Partition);
            message.SetReceiverIdentity(msg.ReceiverIdentity);
            message.SetReceiverNodeIdentity(msg.ReceiverNodeIdentity);
            message.TraceOptions = msg.TraceOptions.ToTraceOptions();
            message.SetDistribution(msg.Distribution.ToDistribution());
            message.SetCallbackReceiverNodeIdentity(msg.CallbackReceiverNodeIdentity);
            message.SetCallbackKey(msg.CallbackKey);
            message.SetDomain(msg.Domain);
            message.SetSignature(msg.Signature);
            message.SetHops(msg.Hops);
            message.CopyMessageRouting(msg.Routing.Select(r => new Core.SocketEndpoint()));
            message.CopyCallbackPoint(msg.CallbackPoint.Select(cb => new Core.MessageIdentifier(cb.Identity, cb.Version, cb.Partition)));
            message.SetCallbackReceiverIdentity(msg.CallbackReceiverIdentity);
            message.SetCorrelationId(msg.CorrelationId);
            message.TTL = msg.TTL;
            message.SetBody(msg.Body);
            message.SetSocketIdentity(msg.SocketIdentity);

            return message;
        }

        public void Dispose()
        {
            consumer.Close();
        }

        public IPerformanceCounter ReceiveRate { get; set; }
    }
}