using System.Collections.Generic;
using kino.Actors;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Sockets;

namespace kino
{
    public interface IComponentsResolver
    {
        IMessageRouter CreateMessageRouter(RouterConfiguration routerConfiguration,
                                           ClusterMembershipConfiguration clusterMembershipConfiguration,
                                           IEnumerable<RendezvousEndpoint> rendezvousEndpoints,
                                           ILogger logger);

        IMessageHub CreateMessageHub(MessageHubConfiguration messageHubConfiguration,
                                     ILogger logger);

        IActorHost CreateActorHost(RouterConfiguration routerConfiguration,
                                   ILogger logger);
    }
}