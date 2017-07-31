using System;
using System.Collections.Concurrent;
using System.Threading;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;

namespace kino.Cluster
{
    public class AutoDiscoverySender : IAutoDiscoverySender
    {
        private readonly IRendezvousCluster rendezvousCluster;
        private readonly ISocketFactory socketFactory;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager;
        private readonly ILogger logger;
        private readonly BlockingCollection<IMessage> outgoingMessages;

        public AutoDiscoverySender(IRendezvousCluster rendezvousCluster,
                                   ISocketFactory socketFactory,
                                   IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                   ILogger logger)
        {
            this.rendezvousCluster = rendezvousCluster;
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
                            //TODO: Block immediately for the response
                            //Otherwise, consider the RS dead and switch to failover partner
                            //sendingSocket.ReceiveMessage(token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private ISocket CreateClusterMonitorSendingSocket()
        {
            var rendezvousServer = rendezvousCluster.GetCurrentRendezvousServer();
            var socket = socketFactory.CreateDealerSocket();
            socket.SendRate = performanceCounterManager.GetCounter(KinoPerformanceCounters.AutoDiscoverySenderSocketSendRate);
            socket.Connect(rendezvousServer.UnicastUri, true);

            return socket;
        }

        public void EnqueueMessage(IMessage message)
            => outgoingMessages.Add(message);
    }
}