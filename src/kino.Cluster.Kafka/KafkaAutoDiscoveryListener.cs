using System;
using System.Threading;
using kino.Connectivity;
using kino.Connectivity.Kafka;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Cluster.Kafka
{
    public class KafkaAutoDiscoveryListener : IAutoDiscoveryListener
    {
        private readonly IKafkaScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly KafkaRendezvousConfiguration config;
        private readonly IKafkaConnectionFactory connectionFactory;
        private readonly ILogger logger;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISendingSocket<IMessage> forwardingSocket;

        public KafkaAutoDiscoveryListener(KafkaRendezvousConfiguration config,
                                          IKafkaConnectionFactory connectionFactory,
                                          ILocalSocketFactory localSocketFactory,
                                          IKafkaScaleOutConfigurationProvider scaleOutConfigurationProvider,
                                          IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                          ILogger logger)

        {
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.config = config;
            this.connectionFactory = connectionFactory;
            this.logger = logger;
            this.performanceCounterManager = performanceCounterManager;
            forwardingSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket);
        }

        public void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var clusterListener = CreateClusterListener())
                {
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = clusterListener.Receive(token);
                            if (message != null)
                            {
                                ProcessIncomingMessage(message);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception err)
                        {
                            logger.Error(err);
                        }
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private IListener CreateClusterListener()
        {
            var listener = connectionFactory.CreateListener();
            listener.Subscribe(config.BrokerName, config.Topic);

            logger.Info($"Connected to Rendezvous {config.Topic}@{config.BrokerName}");

            return listener;
        }

        private void ProcessIncomingMessage(IMessage message)
        {
            var shouldHandle = IsRequestClusterMessageRoutesMessage(message)
                               || IsRequestNodeMessageRoutingMessage(message)
                               || IsUnregisterMessageRoutingMessage(message)
                               || IsRegisterExternalRoute(message)
                               || IsUnregisterNodeMessage(message)
                               || IsDiscoverMessageRouteMessage(message);

            if (shouldHandle)
            {
                forwardingSocket.Send(message);
            }
        }

        private bool IsDiscoverMessageRouteMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.DiscoverMessageRoute))
            {
                var payload = message.GetPayload<DiscoverMessageRouteMessage>();

                return !ThisNodeSocket(payload.RequestorNodeIdentity);
            }

            return false;
        }

        private bool IsRequestClusterMessageRoutesMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.RequestClusterMessageRoutes))
            {
                var payload = message.GetPayload<RequestClusterMessageRoutesMessage>();

                return !ThisNodeSocket(payload.RequestorNodeIdentity);
            }

            return false;
        }

        private bool IsRequestNodeMessageRoutingMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.RequestNodeMessageRoutes))
            {
                var payload = message.GetPayload<RequestNodeMessageRoutesMessage>();

                return ThisNodeSocket(payload.TargetNodeIdentity);
            }

            return false;
        }

        private bool IsUnregisterNodeMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.UnregisterNode))
            {
                var payload = message.GetPayload<UnregisterNodeMessage>();

                return !ThisNodeSocket(payload.ReceiverNodeIdentity);
            }

            return message.Equals(KinoMessages.UnregisterUnreachableNode);
        }

        private bool IsRegisterExternalRoute(IMessage message)
        {
            if (message.Equals(KinoMessages.RegisterExternalMessageRoute))
            {
                var payload = message.GetPayload<RegisterExternalMessageRouteMessage>();

                return !ThisNodeSocket(payload.NodeIdentity);
            }

            return false;
        }

        private bool IsUnregisterMessageRoutingMessage(IMessage message)
        {
            if (message.Equals(KinoMessages.UnregisterMessageRoute))
            {
                var payload = message.GetPayload<UnregisterMessageRouteMessage>();

                return !ThisNodeSocket(payload.ReceiverNodeIdentity);
            }

            return false;
        }

        private bool ThisNodeSocket(byte[] socketIdentity)
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            return Unsafe.ArraysEqual(scaleOutAddress.Identity, socketIdentity);
        }
    }
}