using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using kino.Cluster;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Rendezvous.Configuration;

namespace kino.Rendezvous
{
    public class PartnerNetworkConnectorManager : IPartnerNetworkConnectorManager
    {
        private readonly ISocketFactory socketFactory;
        private readonly ILocalSocketFactory localSocketFactory;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> perfCountersManager;
        private readonly ILogger logger;
        private readonly IPartnerNetworksConfigurationProvider configProvider;
        private readonly ConcurrentDictionary<Thread, CancellationTokenSource> partnerConnectorsThreads;

        public PartnerNetworkConnectorManager(ISocketFactory socketFactory,
                                              ILocalSocketFactory localSocketFactory,
                                              IPerformanceCounterManager<KinoPerformanceCounters> perfCountersManager,
                                              ILogger logger,
                                              IPartnerNetworksConfigurationProvider configProvider)
        {
            this.socketFactory = socketFactory;
            this.localSocketFactory = localSocketFactory;
            this.perfCountersManager = perfCountersManager;
            this.logger = logger;
            this.configProvider = configProvider;
            partnerConnectorsThreads = new ConcurrentDictionary<Thread, CancellationTokenSource>();
        }

        public void StartConnectors()
        {
            foreach (var partnerNetwork in configProvider.PartnerNetworks)
            {
                CreatePartnerNetworkConnector(partnerNetwork);
            }
        }

        private void CreatePartnerNetworkConnector(PartnerClusterConfiguration partnerNetwork)
        {
            var rndCluster = new RendezvousCluster(partnerNetwork.Cluster);
            var connector = new PartnerNetworkConnector(rndCluster,
                                                        partnerNetwork.AllowedDomains,
                                                        socketFactory,
                                                        localSocketFactory,
                                                        partnerNetwork.HeartBeatSilenceBeforeRendezvousFailover,
                                                        perfCountersManager,
                                                        logger);
            var cancellationTokenSource = new CancellationTokenSource();

            using (var gateway = new Barrier(2))
            {
                var thread = default(Thread);
                thread = new Thread(() => connector.StartBlockingListenMessages(() => RestartConnector(thread, partnerNetwork), cancellationTokenSource.Token, gateway))
                         {
                             IsBackground = true
                         };
                if (partnerConnectorsThreads.TryAdd(thread, cancellationTokenSource))
                {
                    thread.Start();
                }
                else
                {
                    throw new DuplicatedKeyException($"Thread with {nameof(thread.ManagedThreadId)}:{thread.ManagedThreadId} already exists!");
                }
                gateway.SignalAndWait();
            }
        }

        private void RestartConnector(Thread thread, PartnerClusterConfiguration partnerNetwork)
        {
            if (partnerConnectorsThreads.TryRemove(thread, out var tokenSource))
            {
                tokenSource.Cancel();
                thread.Join();
                tokenSource.Dispose();
                CreatePartnerNetworkConnector(partnerNetwork);
            }
            else
            {
                logger.Error($"Thread with {nameof(thread.ManagedThreadId)}:{thread.ManagedThreadId} is not found!");
            }
        }

        public void StopConnectors()
        {
            while (partnerConnectorsThreads.Count > 0)
            {
                var thread = partnerConnectorsThreads.Keys.First();
                if (partnerConnectorsThreads.TryRemove(thread, out var tokenSource))
                {
                    tokenSource.Cancel();
                    thread.Join();
                    tokenSource.Dispose();
                }
            }
        }
    }
}