using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Cluster.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Messaging.Messages;

namespace kino.Cluster
{
    public class AutoDiscoveryBaseListener : IAutoDiscoveryListener
    {
        private readonly ILogger logger;
        private readonly IRendezvousCluster rendezvousCluster;
        private readonly ISocketFactory socketFactory;
        private readonly TimeSpan heartBeatSilenceBeforeRendezvousFailover;
        private readonly ManualResetEvent heartBeatReceived;
        private readonly ManualResetEvent newRendezvousConfiguration;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISendingSocket<IMessage> forwardingSocket;

        public AutoDiscoveryBaseListener(IRendezvousCluster rendezvousCluster,
                                         ISocketFactory socketFactory,
                                         TimeSpan heartBeatSilenceBeforeRendezvousFailover,
                                         IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                         ISendingSocket<IMessage> forwardingSocket,
                                         ILogger logger)
        {
            this.logger = logger;
            this.performanceCounterManager = performanceCounterManager;
            this.forwardingSocket = forwardingSocket;
            this.rendezvousCluster = rendezvousCluster;
            this.socketFactory = socketFactory;
            this.heartBeatSilenceBeforeRendezvousFailover = heartBeatSilenceBeforeRendezvousFailover;
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

        protected virtual bool IsDiscoverMessageRouteMessage(IMessage message)
            => message.Equals(KinoMessages.DiscoverMessageRoute);

        protected virtual bool IsRequestClusterMessageRoutesMessage(IMessage message)
            => message.Equals(KinoMessages.RequestClusterMessageRoutes);

        protected virtual bool IsRequestNodeMessageRoutingMessage(IMessage message)
            => message.Equals(KinoMessages.RequestNodeMessageRoutes);

        protected virtual bool IsUnregisterNodeMessage(IMessage message)
            => message.Equals(KinoMessages.UnregisterNode);

        protected virtual bool IsRegisterExternalRoute(IMessage message)
            => message.Equals(KinoMessages.RegisterExternalMessageRoute);

        protected virtual bool IsUnregisterMessageRoutingMessage(IMessage message)
            => message.Equals(KinoMessages.UnregisterMessageRoute);

        private static bool IsHeartBeat(IMessage message)
            => message.Equals(KinoMessages.HeartBeat);

        private static bool IsRendezvousNotLeader(IMessage message)
            => message.Equals(KinoMessages.RendezvousNotLeader);

        private static bool IsRendezvousReconfiguration(IMessage message)
            => message.Equals(KinoMessages.RendezvousConfigurationChanged);
    }
}