using System.Collections.Generic;
using kino.Actors;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Sockets;

namespace kino
{
    public class Composer
    {
        private readonly SocketFactory socketFactory;

        public Composer(SocketConfiguration socketConfiguration)
        {
            socketFactory = new SocketFactory(socketConfiguration);
        }

        public Composer()
            : this(null)
        {
        }

        public IMessageRouter BuildMessageRouter(RouterConfiguration routerConfiguration,
                                                 ClusterMembershipConfiguration clusterMembershipConfiguration,
                                                 IEnumerable<RendezvousEndpoint> rendezvousEndpoints,
                                                 ILogger logger)
        {
            var rendezvousClusterConfigurationReadonlyStorage = new RendezvousClusterConfigurationReadonlyStorage(rendezvousEndpoints);
            var rendezvousCluster = new RendezvousCluster(rendezvousClusterConfigurationReadonlyStorage);
            var clusterMessageSender = new ClusterMessageSender(rendezvousCluster, routerConfiguration, socketFactory, logger);
            var clusterMembership = new ClusterMembership(clusterMembershipConfiguration, logger);
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var routeDiscovery = new RouteDiscovery(clusterMessageSender,
                                                    routerConfiguration,
                                                    clusterMembershipConfiguration,
                                                    logger);
            var clusterMonitorProvider = new ClusterMonitorProvider(clusterMembershipConfiguration,
                                                                    routerConfiguration,
                                                                    clusterMembership,
                                                                    clusterMessageSender,
                                                                    new ClusterMessageListener(rendezvousCluster,
                                                                                               socketFactory,
                                                                                               routerConfiguration,
                                                                                               clusterMessageSender,
                                                                                               clusterMembership,
                                                                                               clusterMembershipConfiguration,
                                                                                               logger),
                                                                    routeDiscovery);
            var internalRoutingTable = new InternalRoutingTable();
            var serviceMessageHandlers = new IServiceMessageHandler[]
                                         {
                                             new ExternalMessageRouteRegistrationHandler(externalRoutingTable, clusterMembership, logger),
                                             new InternalMessageRouteRegistrationHandler(clusterMonitorProvider, internalRoutingTable, logger),
                                             new MessageRouteDiscoveryHandler(clusterMonitorProvider, internalRoutingTable),
                                             new MessageRouteUnregistrationHandler(externalRoutingTable),
                                             new RoutesRegistrationRequestHandler(clusterMonitorProvider, internalRoutingTable),
                                             new RouteUnregistrationHandler(externalRoutingTable, clusterMembership)
                                         };
            return new MessageRouter(socketFactory,
                                     internalRoutingTable,
                                     externalRoutingTable,
                                     routerConfiguration,
                                     clusterMonitorProvider,
                                     serviceMessageHandlers,
                                     clusterMembershipConfiguration,
                                     logger);
        }

        public IMessageHub BuildMessageHub(MessageHubConfiguration messageHubConfiguration, ILogger logger)
        {
            var callbackHandlerStack = new CallbackHandlerStack();

            return new MessageHub(socketFactory,
                                  callbackHandlerStack,
                                  messageHubConfiguration,
                                  logger);
        }

        public IActorHostManager BuildActorHostManager(RouterConfiguration routerConfiguration,
                                                       ILogger logger)
        {
            return new ActorHostManager(socketFactory,
                                        routerConfiguration,
                                        logger);
        }
    }
}