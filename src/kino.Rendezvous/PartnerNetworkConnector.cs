using System;
using kino.Cluster;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;

namespace kino.Rendezvous
{
    public class PartnerNetworkConnector : AutoDiscoveryBaseListener
    {
        public PartnerNetworkConnector(IRendezvousCluster rendezvousCluster,
                                       ISocketFactory socketFactory,
                                       TimeSpan heartBeatSilenceBeforeRendezvousFailover,
                                       IPerformanceCounterManager<KinoPerformanceCounters> performanceCounterManager,
                                       ISendingSocket<IMessage> forwardingSocket,
                                       ILogger logger)
            : base(rendezvousCluster, socketFactory, heartBeatSilenceBeforeRendezvousFailover, performanceCounterManager, forwardingSocket, logger)
        {
        }
    }
}