using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Confluent.Kafka;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Kafka;
using NodeAddress = kino.Messaging.NodeAddress;

namespace kino.Connectivity.Kafka
{
    public class KafkaListener : IListener
    {
        private static readonly IMessageSerializer DefaultSerializer = new ProtobufMessageSerializer();
        private readonly KafkaListenerConfiguration config;
        private readonly string groupId;
        private readonly BlockingCollection<IMessage> receivedMessages;
        private readonly ConcurrentDictionary<string, ConsumerThreadData> consumers;
        private readonly ManualResetEventSlim receiveNewMessages;
        private readonly ManualResetEventSlim messageAvailable;

        public KafkaListener(KafkaListenerConfiguration config,
                             string groupId)
        {
            this.config = config;
            this.groupId = groupId;
            consumers = new ConcurrentDictionary<string, ConsumerThreadData>();
            receiveNewMessages = new ManualResetEventSlim();
            messageAvailable = new ManualResetEventSlim();
            receivedMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
            messageAvailable.Reset();
            receiveNewMessages.Set();
        }

        public void Dispose()
        {
            foreach (var consumerData in consumers.Values)
            {
                consumerData.TokenSource.Cancel();
                consumerData.ConsumingTask.Wait();
            }
        }

        public IMessage Receive(CancellationToken token)
        {
            while (true)
            {
                if (receivedMessages.TryTake(out var message))
                {
                    return message;
                }

                messageAvailable.Reset();

                if (receivedMessages.TryTake(out message))
                {
                    return message;
                }

                receiveNewMessages.Set();
                messageAvailable.Wait(token);
            }
        }

        public void Subscribe(string brokerName, string topic)
        {
            if (consumers.TryGetValue(brokerName, out var consumerData))
            {
                consumerData.Consumer.Subscribe(consumerData.Consumer.Subscription.Concat(topic.ToEnumerable()));
            }
            else
            {
                throw new KeyNotFoundException(brokerName);
            }
        }

        public void Unsubscribe(string brokerName, string topic)
        {
            if (consumers.TryGetValue(brokerName, out var consumerData))
            {
                consumerData.Consumer.Subscribe(consumerData.Consumer.Subscription.Except(topic.ToEnumerable()));
            }
            else
            {
                throw new KeyNotFoundException(brokerName);
            }
        }

        public void Connect(string brokerName)
            => consumers.GetOrAdd(brokerName, CreateConsumer);

        public void Disconnect(string brokerName)
        {
            if (consumers.TryGetValue(brokerName, out var consumerData))
            {
                consumerData.TokenSource.Cancel();
                consumerData.ConsumingTask.Wait();
            }
            else
            {
                throw new KeyNotFoundException(brokerName);
            }
        }

        private ConsumerThreadData CreateConsumer(string brokerName)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var consumer = CreateKafkaConsumer();
            return new ConsumerThreadData
                   {
                       Consumer = consumer,
                       TokenSource = cancellationTokenSource,
                       ConsumingTask = Task.Factory.StartNew(() => PollConsumer(consumer, cancellationTokenSource.Token), TaskCreationOptions.LongRunning)
                   };
        }

        private void PollConsumer(IConsumer<Null, byte[]> consumer, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                var res = consumer.Consume(token);

                consumer.Commit();

                ReceiveRate?.Increment();

                var wireFormatVersion = res.Headers[0].GetValueBytes().GetUShort();

                var msg = DefaultSerializer.Deserialize<WireMessage>(res.Value);

                var message = CreateMessage(msg);

                receivedMessages.Add(message, token);

                messageAvailable.Set();

                receiveNewMessages.Wait(token);
                receiveNewMessages.Reset();
            }
        }

        private void ProcessMessage(IMessage message)
        {
            throw new NotImplementedException();
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
            message.CopyMessageRouting(msg.Routing.Select(r => new NodeAddress
                                                               {
                                                                   Address = r.Address,
                                                                   Identity = r.Identity
                                                               }));
            message.CopyCallbackPoint(msg.CallbackPoint.Select(cb => new Core.MessageIdentifier(cb.Identity, cb.Version, cb.Partition)));
            message.SetCallbackReceiverIdentity(msg.CallbackReceiverIdentity);
            message.SetCorrelationId(msg.CorrelationId);
            message.TTL = msg.TTL;
            message.SetBody(msg.Body);
            message.SetSocketIdentity(msg.SocketIdentity);

            return message;
        }

        private IConsumer<Null, byte[]> CreateKafkaConsumer()
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
            return new ConsumerBuilder<Null, byte[]>(consumerConfig).Build();
        }

        IPerformanceCounter ReceiveRate { get; set; }
    }
}