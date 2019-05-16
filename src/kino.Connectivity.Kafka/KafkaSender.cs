using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Confluent.Kafka;
using kino.Core;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Kafka;
using MessageIdentifier = kino.Messaging.Kafka.MessageIdentifier;

namespace kino.Connectivity.Kafka
{
    public class KafkaSender : ISender
    {
        private readonly KafkaSenderConfiguration config;
        private readonly ConcurrentDictionary<string, IProducer<Null, byte[]>> producers;
        private static readonly IMessageSerializer DefaultSerializer = new ProtobufMessageSerializer();
        private ReceiverIdentifier socketIdentity;
        private static readonly TimeSpan DefaultFlushTimeout = TimeSpan.FromMilliseconds(500);

        public KafkaSender(KafkaSenderConfiguration config)
        {
            this.config = config;
            producers = new ConcurrentDictionary<string, IProducer<Null, byte[]>>();
            socketIdentity = ReceiverIdentifier.Create();
        }

        public void Send(string brokerName, string destination, IMessage message)
        {
            if (producers.TryGetValue(brokerName, out var producer))
            {
                Send(producer, destination, message);
            }
            else
            {
                throw new KeyNotFoundException(brokerName);
            }
        }

        public void Connect(string brokerName)
            => producers.GetOrAdd(brokerName, CreateProducer);

        private IProducer<Null, byte[]> CreateProducer(string _)
            => CreateKafkaProducer();

        public void Disconnect(string brokerName)
        {
            throw new NotImplementedException();
        }

        private IProducer<Null, byte[]> CreateKafkaProducer()
        {
            var consumerConfig = new ProducerConfig
                                 {
                                     BootstrapServers = config.BootstrapServers,
                                     ClientId = $"{Environment.MachineName}-{Process.GetCurrentProcess().Id}",
                                     LingerMs = (int?) config.Linger?.TotalMilliseconds,
                                     LogConnectionClose = false,
                                     SocketKeepaliveEnable = config.SocketKeepAliveEnabled,
                                     ApiVersionRequest = true
                                 };
            return new ProducerBuilder<Null, byte[]>(consumerConfig).Build();
        }

        private void Send(IProducer<Null, byte[]> producer, string destination, IMessage message)
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
                                               .Select(r => new Messaging.Kafka.NodeAddress {Address = r.Address, Identity = r.Identity})
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
            producer.Produce(destination, kafkaMessage);
        }

        public void Dispose()
        {
            foreach (var producer in producers.Values)
            {
                producer.Flush(DefaultFlushTimeout);
            }
        }
    }
}