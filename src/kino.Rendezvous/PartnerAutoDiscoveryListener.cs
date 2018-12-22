using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Rendezvous.Configuration;
using kino.Security;

namespace kino.Rendezvous
{
    public class PartnerAutoDiscoveryListener : IPartnerAutoDiscoveryListener
    {
        private const string AllowAll = "*";
        private readonly ILogger logger;
        private readonly IPartnerRendezvousCluster rendezvousCluster;
        private readonly ISocketFactory socketFactory;
        private readonly TimeSpan heartBeatSilenceBeforeRendezvousFailover;
        private readonly IEnumerable<string> allowedDomains;
        private readonly ISecurityProvider securityProvider;
        private readonly ManualResetEvent heartBeatReceived;
        private readonly ManualResetEvent newRendezvousConfiguration;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISendingSocket<IMessage> forwardingSocket;
        private Task monitoringTask;
        private Task listeningTask;
        private CancellationTokenSource tokenSource;

        public PartnerAutoDiscoveryListener(IPartnerRendezvousCluster rendezvousCluster,
                                            ISocketFactory socketFactory,
                                            ILocalSocketFactory localSocketFactory,
                                            TimeSpan heartBeatSilenceBeforeRendezvousFailover,
                                            IEnumerable<string> allowedDomains,
                                            ISecurityProvider securityProvider,
                                            IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                            ILogger logger)
        {
            this.logger = logger;
            this.performanceCounterManager = performanceCounterManager;
            forwardingSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.PartnerClusterSocket);
            this.rendezvousCluster = rendezvousCluster;
            this.socketFactory = socketFactory;
            this.heartBeatSilenceBeforeRendezvousFailover = heartBeatSilenceBeforeRendezvousFailover;
            this.allowedDomains = allowedDomains;
            this.securityProvider = securityProvider;
            heartBeatReceived = new ManualResetEvent(false);
            newRendezvousConfiguration = new ManualResetEvent(false);
        }

        public void Start()
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            heartBeatReceived.Reset();
            newRendezvousConfiguration.Reset();

            monitoringTask = Task.Factory.StartNew(_ => RendezvousConnectionMonitor(tokenSource.Token),
                                                   TaskCreationOptions.LongRunning,
                                                   tokenSource.Token);
            listeningTask = Task.Factory.StartNew(_ => StartListenMessages(tokenSource.Token),
                                                  TaskCreationOptions.LongRunning,
                                                  tokenSource.Token);
        }

        public void Stop()
        {
            tokenSource.Cancel();
            monitoringTask.Wait();
            listeningTask.Wait();
        }

        private void StartListenMessages(CancellationToken token)
        {
            try
            {
                using (var clusterMonitorSubscriptionSocket = CreateClusterMonitorSubscriptionSocket())
                {
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

        private void RendezvousConnectionMonitor(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (HeartBeatSilence(token))
                    {
                        Stop();
                        Start();
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
                rendezvousCluster.Reconfigure(payload.RendezvousNodes.Select(rn => new PartnerRendezvousEndpoint(rn.BroadcastUri)));
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
                var newLeader = new PartnerRendezvousEndpoint(payload.NewLeader.BroadcastUri);
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
            var shouldHandle = (IsRequestClusterMessageRoutesMessage(message)
                             || IsRequestNodeMessageRoutingMessage(message)
                             || IsUnregisterMessageRoutingMessage(message)
                             || IsRegisterExternalRoute(message)
                             || IsUnregisterNodeMessage(message)
                             || IsDiscoverMessageRouteMessage(message))
                            && DomainIsAllowed(message);

            if (shouldHandle)
            {
                forwardingSocket.Send(message);
            }

            return shouldHandle;
        }

        private bool DomainIsAllowed(IMessage message)
            => allowedDomains.Contains(AllowAll)
            || securityProvider.DomainIsAllowed(message.Domain);

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