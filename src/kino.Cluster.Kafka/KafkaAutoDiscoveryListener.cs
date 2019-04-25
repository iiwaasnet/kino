﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Cluster.Kafka
{
    public class KafkaAutoDiscoveryListener : IAutoDiscoveryListener
    {
        private readonly IScaleOutConfigurationProvider scaleOutConfigurationProvider;
        private readonly ILogger logger;
        private readonly IRendezvousCluster rendezvousCluster;
        private readonly ISocketFactory socketFactory;
        private readonly TimeSpan heartBeatSilenceBeforeRendezvousFailover;
        private readonly ManualResetEvent heartBeatReceived;
        private readonly ManualResetEvent newRendezvousConfiguration;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISendingSocket<IMessage> forwardingSocket;

        public KafkaAutoDiscoveryListener(IRendezvousCluster rendezvousCluster,
                                     ISocketFactory socketFactory,
                                     ILocalSocketFactory localSocketFactory,
                                     IScaleOutConfigurationProvider scaleOutConfigurationProvider,
                                     ClusterMembershipConfiguration membershipConfiguration,
                                     IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                     ILogger logger)

        {
            this.scaleOutConfigurationProvider = scaleOutConfigurationProvider;
            this.logger = logger;
            this.performanceCounterManager = performanceCounterManager;
            forwardingSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.RouterLocalSocket);
            this.rendezvousCluster = rendezvousCluster;
            this.socketFactory = socketFactory;
            heartBeatSilenceBeforeRendezvousFailover = membershipConfiguration.HeartBeatSilenceBeforeRendezvousFailover;
            heartBeatReceived = new ManualResetEvent(false);
            newRendezvousConfiguration = new ManualResetEvent(false);
        }

        public void StartBlockingListenMessages(Action restartRequestHandler, CancellationToken token, Barrier gateway)
        {
            try
            {
                StartRendezvousMonitoring(restartRequestHandler, token);

                using (var clusterMonitorSubscriptionSocket = CreateClusterMonitorSubscriptionSocket())
                {
                    gateway.SignalAndWait(token);

                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = clusterMonitorSubscriptionSocket.Receive(token);
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

        private void StartRendezvousMonitoring(Action restartRequestHandler, CancellationToken token)
        {
            heartBeatReceived.Reset();
            newRendezvousConfiguration.Reset();

            Task.Factory.StartNew(_ => RendezvousConnectionMonitor(restartRequestHandler, token),
                                  TaskCreationOptions.LongRunning,
                                  token);
        }

        private void RendezvousConnectionMonitor(Action restartRequestHandler, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (HeartBeatSilence(token))
                    {
                        restartRequestHandler();
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

        private bool HeartBeatSilence(CancellationToken token)
        {
            const int rendezvousConfigurationChanged = 1;
            const int cancellationRequested = 2;
            var result = WaitHandle.WaitAny(new[]
                                            {
                                                heartBeatReceived,
                                                newRendezvousConfiguration,
                                                token.WaitHandle
                                            },
                                            heartBeatSilenceBeforeRendezvousFailover);
            switch (result)
            {
                case WaitHandle.WaitTimeout:
                    var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
                    logger.Info($"HeartBeat timeout Rendezvous {rendezvousServer.BroadcastUri}");
                    rendezvousCluster.RotateRendezvousServers();
                    return true;
                case rendezvousConfigurationChanged:
                    newRendezvousConfiguration.Reset();
                    return true;
                case cancellationRequested:
                    return false;
            }

            heartBeatReceived.Reset();
            return false;
        }

        private ISocket CreateClusterMonitorSubscriptionSocket()
        {
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.AutoDiscoveryListenerSocketReceiveRate);
            socket.Connect(rendezvousServer.BroadcastUri, true);
            socket.Subscribe();

            logger.Info($"Connected to Rendezvous {rendezvousServer.BroadcastUri}");

            return socket;
        }

        private bool ProcessIncomingMessage(IMessage message)
            => HeartBeat(message)
            || Pong(message)
            || RendezvousReconfiguration(message)
            || RoutingControlMessage(message)
            || RendezvousNotLeader(message);

        private bool RendezvousReconfiguration(IMessage message)
        {
            var shouldHandle = IsRendezvousReconfiguration(message);
            if (shouldHandle)
            {
                var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
                logger.Info($"New Rendezvous cluster configuration. Disconnecting {rendezvousServer.BroadcastUri}");

                var payload = message.GetPayload<RendezvousConfigurationChangedMessage>();
                rendezvousCluster.Reconfigure(payload.RendezvousNodes.Select(rn => new RendezvousEndpoint(rn.UnicastUri, rn.BroadcastUri)));
                newRendezvousConfiguration.Set();
            }

            return shouldHandle;
        }

        private bool RendezvousNotLeader(IMessage message)
        {
            var shouldHandle = IsRendezvousNotLeader(message);
            if (shouldHandle)
            {
                var payload = message.GetPayload<RendezvousNotLeaderMessage>();
                var newLeader = new RendezvousEndpoint(payload.NewLeader.UnicastUri, payload.NewLeader.BroadcastUri);
                var currentLeader = rendezvousCluster.GetCurrentRendezvousServer();
                if (!currentLeader.Equals(newLeader))
                {
                    logger.Info($"New Rendezvous leader: {newLeader.BroadcastUri}. " +
                                $"Disconnecting {currentLeader.BroadcastUri}");

                    if (!rendezvousCluster.SetCurrentRendezvousServer(newLeader))
                    {
                        logger.Error($"New Rendezvous leader {newLeader.BroadcastUri} "
                                   + $"was not found within configured Rendezvous cluster: [{string.Join(",", rendezvousCluster.Nodes.Select(n => n.BroadcastUri))}]");
                    }

                    newRendezvousConfiguration.Set();
                }
            }

            return shouldHandle;
        }

        private bool HeartBeat(IMessage message)
        {
            var shouldHandle = IsHeartBeat(message);
            if (shouldHandle)
            {
                heartBeatReceived.Set();
            }

            return shouldHandle;
        }

        private static bool Pong(IMessage message)
            => message.Equals(KinoMessages.Pong);

        private bool RoutingControlMessage(IMessage message)
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

            return shouldHandle;
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

        private static bool IsHeartBeat(IMessage message)
            => message.Equals(KinoMessages.HeartBeat);

        private static bool IsRendezvousNotLeader(IMessage message)
            => message.Equals(KinoMessages.RendezvousNotLeader);

        private static bool IsRendezvousReconfiguration(IMessage message)
            => message.Equals(KinoMessages.RendezvousConfigurationChanged);

        private bool ThisNodeSocket(byte[] socketIdentity)
        {
            var scaleOutAddress = scaleOutConfigurationProvider.GetScaleOutAddress();
            return Unsafe.ArraysEqual(scaleOutAddress.Identity, socketIdentity);
        }
    }
}