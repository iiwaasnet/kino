using System;
using System.Diagnostics;
using System.Linq;
using Confluent.Kafka;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Kafka;
using MessageIdentifier = kino.Messaging.Kafka.MessageIdentifier;
using SocketEndpoint = kino.Messaging.Kafka.SocketEndpoint;

namespace kino.Connectivity.Kafka
{
    public class KafkaSender : ISender
    {
        private static readonly IMessageSerializer DefaultSerializer = new ProtobufMessageSerializer();
        private readonly IProducer<Null, byte[]> sender;
        private ReceiverIdentifier socketIdentity;
        private static readonly TimeSpan DefaultFlushTimeout = TimeSpan.FromMilliseconds(500);

        public KafkaSender(KafkaSenderConfiguration config)
        {
            socketIdentity = ReceiverIdentifier.Create();
            var consumerConfig = new ProducerConfig
                                 {
                                     BootstrapServers = config.BootstrapServers,
                                     ClientId = $"{Environment.MachineName}-{Process.GetCurrentProcess().Id}",
                                     LingerMs = (int?) config.Linger?.TotalMilliseconds,
                                     LogConnectionClose = false,
                                     SocketKeepaliveEnable = config.SocketKeepAliveEnabled,
                                     ApiVersionRequest = true
                                 };
            sender = new ProducerBuilder<Null, byte[]>(consumerConfig).Build();
        }

        public void Send(string destination, IMessage message)
        {
            var msg = message.As<Message>();

            var wireMessage = new WireMessage
                              {
                                  Identity = msg.Identity,
                                  Partition = msg.Partition,
                                  Version = msg.Version,
                                  Body = msg.Body,
                                  ReceiverIdentity = msg.ReceiverIdentity,
                                  ReceiverNodeIdentity = msg.ReceiverNodeIdentity,
                                  TraceOptions = msg.TraceOptions.ToTraceOptionsCode(),
                                  Distribution = msg.Distribution.ToDistributionCode(),
                                  CallbackReceiverNodeIdentity = msg.CallbackReceiverNodeIdentity,
                                  CallbackKey = msg.CallbackKey,
                                  Domain = msg.Domain,
                                  Signature = msg.Signature,
                                  Hops = msg.Hops,
                                  Routing = msg.GetMessageRouting()
                                               .Select(r => new SocketEndpoint {BrokerUri = r.Uri, Identity = r.Identity})
                                               .ToList(),
                                  CallbackPoint = msg.CallbackPoint
                                                     .Select(cp => new MessageIdentifier
                                                                   {
                                                                       Partition = cp.Partition,
                                                                       Identity = cp.Identity,
                                                                       Version = cp.Version
                                                                   })
                                                     .ToList(),
                                  CallbackReceiverIdentity = msg.CallbackReceiverIdentity,
                                  CorrelationId = msg.CorrelationId,
                                  TTL = msg.TTL,
                                  SocketIdentity = msg.SocketIdentity
                              };
            var kafkaMessage = new Message<Null, byte[]>
                               {
                                   Value = DefaultSerializer.Serialize(wireMessage),
                                   Headers = new Headers
                                             {
                                                 {KafkaMessageHeaders.DestinationNodeIdentity, msg.SocketIdentity}
                                             }
                               };
            sender.Produce(destination, kafkaMessage);
        }

        public void Dispose()
        {
            sender.Flush(DefaultFlushTimeout);
        }
    }
}