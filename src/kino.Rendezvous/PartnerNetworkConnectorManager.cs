using System.Collections.Generic;
using System.Linq;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Framework;
using kino.Rendezvous.Configuration;
using kino.Security;

namespace kino.Rendezvous
{
    public class PartnerNetworkConnectorManager : IPartnerNetworkConnectorManager
    {
        private readonly ISocketFactory socketFactory;
        private readonly ILocalSocketFactory localSocketFactory;
        private readonly IPerformanceCounterManager<KinoPerformanceCounters> perfCountersManager;
        private readonly ILogger logger;
        private readonly IPartnerNetworksConfigurationProvider configProvider;
        private IEnumerable<PartnerAutoDiscoveryListener> partnerConnectors;

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
        }

        public void StartConnectors()
        {
            partnerConnectors = configProvider.PartnerNetworks
                                              .Select(CreatePartnerNetworkConnector)
                                              .ToList();
            partnerConnectors.ExecuteForEach(pc => pc.Start());
        }

        private PartnerAutoDiscoveryListener CreatePartnerNetworkConnector(PartnerClusterConfiguration partnerNetwork)
            => new PartnerAutoDiscoveryListener(new PartnerRendezvousCluster(partnerNetwork.Cluster),
                                                socketFactory,
                                                localSocketFactory,
                                                partnerNetwork.HeartBeatSilenceBeforeRendezvousFailover,
                                                partnerNetwork.AllowedDomains,
                                                perfCountersManager,
                                                logger);

        public void StopConnectors()
            => partnerConnectors.ExecuteForEach(pc => pc.Stop());
    }
}