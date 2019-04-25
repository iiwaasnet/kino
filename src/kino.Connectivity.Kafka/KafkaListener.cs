using System;
using System.Diagnostics;
using System.Threading;
using Confluent.Kafka;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;

namespace kino.Connectivity.Kafka
{
    public class KafkaListener : IListener
    {
        private static readonly IMessageSerializer DefaultSerializer = new ProtobufMessageSerializer();

        private readonly IConsumer<Null, byte[]> consumer;

        public KafkaListener(KafkaListenerConfiguration config,
                             string groupId,
                             string topic)
        {
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
            consumer = new ConsumerBuilder<Null, byte[]>(consumerConfig)
                .Build();
            consumer.Subscribe(topic);
        }

        public IMessage Receive(CancellationToken token)
        {
            var res = consumer.Consume(token);
            consumer.Commit();

            ReceiveRate?.Increment();

            var wireFormatVersion = res.Headers[0].GetValueBytes().GetUShort();

            var msg = DefaultSerializer.Deserialize<WireMessage>(res.Value);

            var message = new Message(msg.Identity, msg.Version, msg.Partition);
        }

        public void Dispose()
        {
            consumer.Close();
        }

        public IPerformanceCounter ReceiveRate { get; set; }
    }
}