using System;
using System.Collections.Generic;
using kino.Cluster;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;

namespace kino.Rendezvous
{
    public class PartnerNetworkConnector : AutoDiscoveryBaseListener
    {
        private readonly IEnumerable<string> allowedDomains;

        public PartnerNetworkConnector(IRendezvousCluster rendezvousCluster,
                                       IEnumerable<string> allowedDomains,
                                       ISocketFactory socketFactory,
                                       ILocalSocketFactory localSocketFactory,
                                       TimeSpan heartBeatSilenceBeforeRendezvousFailover,
                                       IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                       ILogger logger)
            : base(rendezvousCluster,
                   socketFactory,
                   heartBeatSilenceBeforeRendezvousFailover,
                   performanceCounterManager,
                   localSocketFactory.CreateNamed<IMessage>(NamedSockets.PartnerClusterSocket),
                   logger)
            => this.allowedDomains = allowedDomains;
    }
}