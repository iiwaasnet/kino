using System;
using System.Collections.Concurrent;
using System.Threading;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Core.Messaging;
using kino.Core.Messaging.Messages;
using kino.Core.Sockets;

namespace kino.Core.Connectivity
{
    public class ClusterMessageSender : IClusterMessageSender
    {
        private readonly IRendezvousCluster rendezvousCluster;
        private readonly RouterConfiguration routerConfiguration;
        private readonly ISocketFactory socketFactory;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ILogger logger;
        private readonly BlockingCollection<IMessage> outgoingMessages;

        public ClusterMessageSender(IRendezvousCluster rendezvousCluster,
                                    RouterConfiguration routerConfiguration,
                                    ISocketFactory socketFactory,
                                    IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                    ILogger logger)
        {
            this.rendezvousCluster = rendezvousCluster;
            this.routerConfiguration = routerConfiguration;
            this.socketFactory = socketFactory;
            this.performanceCounterManager = performanceCounterManager;
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
                    try
                    {
                        foreach (var messageOut in outgoingMessages.GetConsumingEnumerable(token))
                        {
                            clusterMonitorSendingSocket.SendMessage(messageOut);
                            // TODO: Block immediatelly for the response
                            // Otherwise, consider the RS dead and switch to failover partner
                            //sendingSocket.ReceiveMessage(token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    clusterMonitorSendingSocket.SendMessage(CreateUnregisterRoutingMessage());
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private IMessage CreateUnregisterRoutingMessage()
            => Message.Create(new UnregisterNodeMessageRouteMessage
                              {
                                  Uri = routerConfiguration.ScaleOutAddress.Uri.ToSocketAddress(),
                                  SocketIdentity = routerConfiguration.ScaleOutAddress.Identity
                              });

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