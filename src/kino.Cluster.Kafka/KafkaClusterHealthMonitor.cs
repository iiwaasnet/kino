using System;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Connectivity.Kafka;
using kino.Core;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Cluster.Kafka
{
    public class KafkaClusterHealthMonitor : IKafkaClusterHealthMonitor
    {
        private readonly ILogger logger;
        private readonly IListener listener;
        private Task receivingMessages;
        private CancellationTokenSource cancellationTokenSource;

        public KafkaClusterHealthMonitor(IKafkaConnectionFactory connectionFactory,
                                         ILogger logger)
        {
            this.logger = logger;
            listener = connectionFactory.CreateListener();
        }

        public void StartPeerMonitoring(KafkaNode peer, Health health)
        {
            listener.Connect(peer.BrokerName);
            listener.Subscribe(peer.BrokerName, health.Topic);
        }

        public void ScheduleConnectivityCheck(ReceiverIdentifier nodeIdentifier)
        {
            throw new NotImplementedException();
        }

        public void AddPeer(KafkaNode peer, Health health)
            => StartPeerMonitoring(peer, health);

        public void DisconnectFromBroker(string brokerName)
            => listener.Disconnect(brokerName);

        public void Start()
        {
            cancellationTokenSource = new CancellationTokenSource();
            receivingMessages = Task.Factory.StartNew(_ => ReceiveMessages(cancellationTokenSource.Token), TaskCreationOptions.LongRunning, cancellationTokenSource.Token);
        }

        private void ReceiveMessages(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var message = listener.Receive(token);
                    if (message != null)
                    {
                        ProcessHeartBeatMessage(message);
                    }
                }
                catch (Exception err)
                {
                    logger.Error(err);
                }
            }
        }

        private void ProcessHeartBeatMessage(IMessage message)
        {
            var shouldHandle = message.Equals(KinoMessages.HeartBeat);
            if (shouldHandle)
            {
                var payload = message.GetPayload<HeartBeatMessage>();
                var socketIdentifier = new ReceiverIdentifier(payload.SocketIdentity);
                var meta = connectedPeerRegistry.Find(socketIdentifier);
                if (meta != null)
                {
                    meta.LastKnownHeartBeat = DateTime.UtcNow;
                    //logger.Debug($"Received HeartBeat from node {socketIdentifier}");
                }
                else
                {
                    //TODO: Send DiscoveryMessage? What if peer is not supporting message Domains to be used by this node?
                    logger.Warn($"HeartBeat came from unknown node {payload.SocketIdentity.GetAnyString()}. Will disconnect from HealthUri: {payload.HealthUri}");
                    try
                    {
                        socket.Disconnect(payload.HealthUri);
                    }
                    catch (Exception err)
                    {
                        logger.Error(err);
                    }
                }
            }
        }


        public void Stop()
        {
            throw new NotImplementedException();
        }
    }
}