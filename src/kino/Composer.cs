using System.Collections.Generic;
using kino.Actors;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Core.Security;
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
            var securityProvider = new NullSecurityProvider();
            var rendezvousClusterConfigurationReadonlyStorage = new RendezvousClusterConfigurationReadonlyStorage(rendezvousEndpoints);
            var rendezvousCluster = new RendezvousCluster(rendezvousClusterConfigurationReadonlyStorage);
            var clusterMessageSender = new ClusterMessageSender(rendezvousCluster, routerConfiguration, socketFactory, null, securityProvider, logger);
            var clusterMembership = new ClusterMembership(clusterMembershipConfiguration, logger);
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var routeDiscovery = new RouteDiscovery(clusterMessageSender,
                                                    routerConfiguration,
                                                    clusterMembershipConfiguration,
                                                    securityProvider,
                                                    logger);
            var instanceNameResolver = new InstanceNameResolver();
            var performanceCounterManager = new PerformanceCounterManager<KinoPerformanceCounters>(instanceNameResolver, logger);
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
                                                                                               performanceCounterManager,
                                                                                               securityProvider,
                                                                                               logger),
                                                                    routeDiscovery,
                                                                    securityProvider);
            var internalRoutingTable = new InternalRoutingTable();
            var serviceMessageHandlers = new IServiceMessageHandler[]
                                         {
                                             new ExternalMessageRouteRegistrationHandler(externalRoutingTable, clusterMembership, routerConfiguration, securityProvider, logger),
                                             new InternalMessageRouteRegistrationHandler(clusterMonitorProvider, internalRoutingTable, logger),
                                             new MessageRouteDiscoveryHandler(clusterMonitorProvider, internalRoutingTable, securityProvider),
                                             new MessageRouteUnregistrationHandler(externalRoutingTable, securityProvider),
                                             new ClusterMessageRoutesRequestHandler(clusterMonitorProvider, internalRoutingTable, securityProvider),
                                             new NodeMessageRoutesRequestHandler(clusterMonitorProvider, internalRoutingTable, securityProvider),
                                             new NodeUnregistrationHandler(externalRoutingTable, clusterMembership, securityProvider),
                                             new UnreachableNodeUnregistrationHandler(externalRoutingTable, clusterMembership)
                                         };
            return new MessageRouter(socketFactory,
                                     internalRoutingTable,
                                     externalRoutingTable,
                                     routerConfiguration,
                                     clusterMonitorProvider,
                                     serviceMessageHandlers,
                                     clusterMembershipConfiguration,
                                     performanceCounterManager,
                                     securityProvider,
                                     logger);
        }

        public IMessageHub BuildMessageHub(MessageHubConfiguration messageHubConfiguration, ILogger logger)
        {
            var callbackHandlerStack = new CallbackHandlerStack();

            return new MessageHub(socketFactory,
                                  callbackHandlerStack,
                                  messageHubConfiguration,
                                  null,
                                  logger);
        }

        public IActorHostManager BuildActorHostManager(RouterConfiguration routerConfiguration,
                                                       ILogger logger)
        {
            return new ActorHostManager(socketFactory,
                                        routerConfiguration,
                                        null,
                                        logger);
        }
    }
}