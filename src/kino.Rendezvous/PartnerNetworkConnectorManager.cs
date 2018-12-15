using System;
using System.Threading;
using kino.Cluster;
using kino.Cluster.Configuration;
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
        private readonly PartnerNetworksConfiguration config;

        public PartnerNetworkConnectorManager(ISocketFactory socketFactory,
                                              ILocalSocketFactory localSocketFactory,
                                              IPerformanceCounterManager<KinoPerformanceCounters> perfCountersManager,
                                              ILogger logger,
                                              PartnerNetworksConfiguration config)
        {
            this.socketFactory = socketFactory;
            this.localSocketFactory = localSocketFactory;
            this.perfCountersManager = perfCountersManager;
            this.logger = logger;
            this.config = config;
        }

        public void StartConnectors()
        {
            foreach (var partnerNetwork in config.Partners)
            {
                CreatePartnerNetworkConnector(partnerNetwork);
            }
        }

        private void CreatePartnerNetworkConnector(PartnerNetworkConfiguration partnerNetwork)
        {
            var storage = new PartnerNetworksConfigurationReadonlyStorage(partnerNetwork);
            var rndCluster = new RendezvousCluster(storage.As<IConfigurationStorage<RendezvousClusterConfiguration>>());
            var connector = new PartnerNetworkConnector(rndCluster,
                                                        partnerNetwork.AllowedDomains,
                                                        socketFactory,
                                                        localSocketFactory,
                                                        partnerNetwork.HeartBeatSilenceBeforeRendezvousFailover,
                                                        perfCountersManager,
                                                        logger);
            var cancellationTokenSource = new CancellationTokenSource();
            var thread = default(Thread);
            thread = new Thread(() => connector.StartBlockingListenMessages(() => RestartConnector(cancellationTokenSource, thread, partnerNetwork), cancellationTokenSource.Token, null))
                     {
                         IsBackground = true
                     };
            thread.Start();
        }

        private void RestartConnector(CancellationTokenSource tokenSource, Thread thread, PartnerNetworkConfiguration partnerNetwork)
        {
            tokenSource.Cancel();
            thread.Join();
            tokenSource.Dispose();
            CreatePartnerNetworkConnector(partnerNetwork);
        }

        public void StopConnectors()
        {
            throw new NotImplementedException();
        }
    }
}