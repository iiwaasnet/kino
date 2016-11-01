using System;
using System.Collections.Concurrent;
using System.Threading;
using kino.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Messaging;
using kino.Messaging.Messages;
using kino.Security;

namespace kino.Cluster
{
    public class AutoDiscoverySender : IAutoDiscoverySender
    {
        private readonly IRendezvousCluster rendezvousCluster;
        private readonly IRouterConfigurationProvider routerConfigurationProvider;
        private readonly ISocketFactory socketFactory;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ISecurityProvider securityProvider;
        private readonly ILogger logger;
        private readonly BlockingCollection<IMessage> outgoingMessages;
        //TODO: Move to config
        private readonly TimeSpan UnregisterMessageSendTimeout = TimeSpan.FromMilliseconds(500);

        public AutoDiscoverySender(IRendezvousCluster rendezvousCluster,
                                    IRouterConfigurationProvider routerConfigurationProvider,
                                    ISocketFactory socketFactory,
                                    IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                    ISecurityProvider securityProvider,
                                    ILogger logger)
        {
            this.rendezvousCluster = rendezvousCluster;
            this.routerConfigurationProvider = routerConfigurationProvider;
            this.socketFactory = socketFactory;
            this.performanceCounterManager = performanceCounterManager;
            this.securityProvider = securityProvider;
            this.logger = logger;
            outgoingMessages = new BlockingCollection<IMessage>(new ConcurrentQueue<IMessage>());
        }

        public void StartBlockingSendMessages(CancellationToken token, Barrier gateway)
        {
            try
            {
                using (var clusterMonitorSendingSocket = CreateClusterMonitorSendingSocket())
                {
                    gateway.SignalAndWait(token);
                    //TODO: In case of Rendezvous leader change, this will lead to storm of cluster routes discovery messages
                    //TODO: Check if this call really needed
                    RequestClusterRoutes(clusterMonitorSendingSocket);

                    try
                    {
                        foreach (var messageOut in outgoingMessages.GetConsumingEnumerable(token))
                        {
                            clusterMonitorSendingSocket.SendMessage(messageOut);
                            //TODO: Block immediately for the response
                            //Otherwise, consider the RS dead and switch to failover partner
                            //sendingSocket.ReceiveMessage(token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    UnregisterRoutingSelf(clusterMonitorSendingSocket);
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void RequestClusterRoutes(ISocket clusterMonitorSendingSocket)
        {
            try
            {
                var scaleOutAddress = routerConfigurationProvider.GetScaleOutAddress();
                foreach (var domain in securityProvider.GetAllowedDomains())
                {
                    var message = Message.Create(new RequestClusterMessageRoutesMessage
                    {
                        RequestorSocketIdentity = scaleOutAddress.Identity,
                        RequestorUri = scaleOutAddress.Uri.ToSocketAddress()
                    },
                                                 domain);
                    message.As<Message>().SignMessage(securityProvider);

                    clusterMonitorSendingSocket.SendMessage(message);

                    logger.Info($"Request to discover cluster routes for Domain [{domain}] sent.");
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private void UnregisterRoutingSelf(ISocket clusterMonitorSendingSocket)
        {
            var scaleOutAddress = routerConfigurationProvider.GetScaleOutAddress();
            foreach (var domain in securityProvider.GetAllowedDomains())
            {
                var message = Message.Create(new UnregisterNodeMessage
                                             {
                                                 Uri = scaleOutAddress.Uri.ToSocketAddress(),
                                                 SocketIdentity = scaleOutAddress.Identity,
                                             },
                                             domain);
                message.As<Message>().SignMessage(securityProvider);

                clusterMonitorSendingSocket.SendMessage(message);
            }
            
            UnregisterMessageSendTimeout.Sleep();
        }

        private ISocket CreateClusterMonitorSendingSocket()
        {
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateDealerSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.ClusterSenderSocketSendRate);
            socket.Connect(rendezvousServer.UnicastUri);

            return socket;
        }

        public void EnqueueMessage(IMessage message)
        {
            outgoingMessages.Add(message);
        }
    }
}