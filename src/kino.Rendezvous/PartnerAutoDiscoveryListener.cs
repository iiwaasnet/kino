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
        private readonly HashSet<string> allowedDomains;
        private readonly ManualResetEvent heartBeatReceived;
        private readonly ManualResetEvent newRendezvousConfiguration;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISendingSocket<IMessage> forwardingSocket;
        private Task task;
        private CancellationTokenSource tokenSource;

        public PartnerAutoDiscoveryListener(IPartnerRendezvousCluster rendezvousCluster,
                                            ISocketFactory socketFactory,
                                            ILocalSocketFactory localSocketFactory,
                                            TimeSpan heartBeatSilenceBeforeRendezvousFailover,
                                            IEnumerable<string> allowedDomains,
                                            IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                            ILogger logger)
        {
            this.logger = logger;
            this.performanceCounterManager = performanceCounterManager;
            forwardingSocket = localSocketFactory.CreateNamed<IMessage>(NamedSockets.PartnerClusterSocket);
            this.rendezvousCluster = rendezvousCluster;
            this.socketFactory = socketFactory;
            this.heartBeatSilenceBeforeRendezvousFailover = heartBeatSilenceBeforeRendezvousFailover;
            this.allowedDomains = new HashSet<string>(allowedDomains);
            heartBeatReceived = new ManualResetEvent(false);
            newRendezvousConfiguration = new ManualResetEvent(false);
        }

        public void Start()
        {
            tokenSource?.Dispose();
            tokenSource = new CancellationTokenSource();
            heartBeatReceived.Reset();
            newRendezvousConfiguration.Reset();

            task = Task.Factory.StartNew(_ => RunListener(tokenSource.Token),
                                         TaskCreationOptions.LongRunning,
                                         tokenSource.Token);
        }

        public void Stop()
        {
            tokenSource.Cancel();
            task.Wait();
        }

        private void RunListener(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var tokenSource = new CancellationTokenSource())
                    {
                        using (var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(token, tokenSource.Token))
                        {
                            var (monitoring, listening) = StartAutoDiscovery(linkedTokenSource.Token);
                            monitoring.Wait(linkedTokenSource.Token);
                            linkedTokenSource.Cancel();
                            listening.Wait(token);
                        }
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

        private (Task Montoring, Task Listening) StartAutoDiscovery(CancellationToken token)
        {
            heartBeatReceived.Reset();
            newRendezvousConfiguration.Reset();

            var monitoringTask = Task.Factory.StartNew(_ => RendezvousConnectionMonitor(token),
                                                       TaskCreationOptions.LongRunning,
                                                       token);
            var listeningTask = Task.Factory.StartNew(_ => ListenMessages(token),
                                                      TaskCreationOptions.LongRunning,
                                                      token);

            return (monitoringTask, listeningTask);
        }

        private void ListenMessages(CancellationToken token)
        {
            try
            {
                using (var partnerClusterSubscriptionSocket = CreatePartnerClusterSubscriptionSocket())
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var message = partnerClusterSubscriptionSocket.Receive(token);
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
            try
            {
                while (!token.IsCancellationRequested && !HeartBeatSilence(token))
                {
                    ;
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
                    logger.Info($"HeartBeat timeout Partner Rendezvous {rendezvousServer.BroadcastUri}");
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

        private ISocket CreatePartnerClusterSubscriptionSocket()
        {
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateSubscriberSocket();
            socket.ReceiveRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.AutoDiscoveryListenerSocketReceiveRate);
            socket.Connect(rendezvousServer.BroadcastUri, true);
            socket.Subscribe();

            logger.Info($"Connected to Partner Rendezvous {rendezvousServer.BroadcastUri}");

            return socket;
        }

        private bool ProcessIncomingMessage(IMessage message)
            => HeartBeat(message)
            || Pong(message)
            || RendezvousReconfiguration(message)
            || RendezvousNotLeader(message)
            || RoutingControlMessage(message);

        private bool RendezvousReconfiguration(IMessage message)
        {
            var shouldHandle = IsRendezvousReconfiguration(message);
            if (shouldHandle)
            {
                var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
                logger.Info($"New Partner Rendezvous cluster configuration. Disconnecting {rendezvousServer.BroadcastUri}");

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
                    logger.Info($"New Partner Rendezvous leader: {newLeader.BroadcastUri}. " +
                                $"Disconnecting {currentLeader.BroadcastUri}");

                    if (!rendezvousCluster.SetCurrentRendezvousServer(newLeader))
                    {
                        logger.Error($"New Rendezvous leader {newLeader.BroadcastUri} "
                                   + $"was not found within configured Partner Rendezvous cluster: [{string.Join(",", rendezvousCluster.Nodes.Select(n => n.BroadcastUri))}]");
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
            || allowedDomains.Contains(message.Domain);

        private bool IsDiscoverMessageRouteMessage(IMessage message)
            => message.Equals(KinoMessages.DiscoverMessageRoute);

        private bool IsRequestClusterMessageRoutesMessage(IMessage message)
            => message.Equals(KinoMessages.RequestClusterMessageRoutes);

        private bool IsRequestNodeMessageRoutingMessage(IMessage message)
            => message.Equals(KinoMessages.RequestNodeMessageRoutes);

        private bool IsUnregisterNodeMessage(IMessage message)
            => message.Equals(KinoMessages.UnregisterNode);

        private bool IsRegisterExternalRoute(IMessage message)
            => message.Equals(KinoMessages.RegisterExternalMessageRoute);

        private bool IsUnregisterMessageRoutingMessage(IMessage message)
            => message.Equals(KinoMessages.UnregisterMessageRoute);

        private bool IsHeartBeat(IMessage message)
            => message.Equals(KinoMessages.HeartBeat);

        private bool IsRendezvousNotLeader(IMessage message)
            => message.Equals(KinoMessages.RendezvousNotLeader);

        private bool IsRendezvousReconfiguration(IMessage message)
            => message.Equals(KinoMessages.RendezvousConfigurationChanged);
    }
}