using System.Collections.Generic;
using kino.Actors;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Connectivity.ServiceMessageHandlers;
using kino.Core.Diagnostics;
using kino.Core.Framework;
using kino.Core.Sockets;
using MessageTracer = kino.Core.Connectivity.MessageTracer;

namespace kino
{
    public class ComponentsResolver : IComponentsResolver
    {
        private readonly SocketFactory socketFactory;

        public ComponentsResolver(SocketConfiguration socketConfiguration)
        {
            socketFactory = new SocketFactory(socketConfiguration);
        }

        public IMessageRouter CreateMessageRouter(RouterConfiguration routerConfiguration,
                                                  ClusterMembershipConfiguration clusterMembershipConfiguration,
                                                  IEnumerable<RendezvousEndpoint> rendezvousEndpoints,
                                                  ILogger logger)
        {
            var rendezvousClusterConfigurationReadonlyStorage = new RendezvousClusterConfigurationReadonlyStorage(rendezvousEndpoints);
            var rendezvousCluster = new RendezvousCluster(rendezvousClusterConfigurationReadonlyStorage);
            var clusterMessageSender = new ClusterMessageSender(rendezvousCluster, routerConfiguration, socketFactory, logger);
            var clusterMembership = new ClusterMembership(clusterMembershipConfiguration, logger);
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var clusterMonitor = new ClusterMonitorProvider(clusterMembershipConfiguration,
                                                            routerConfiguration,
                                                            clusterMembership,
                                                            clusterMessageSender,
                                                            new ClusterMessageListener(rendezvousCluster,
                                                                                       socketFactory,
                                                                                       routerConfiguration,
                                                                                       clusterMessageSender,
                                                                                       clusterMembership,
                                                                                       clusterMembershipConfiguration,
                                                                                       logger))
                .GetClusterMonitor();
            var internalRoutingTable = new InternalRoutingTable();
            var serviceMessageHandlers = new IServiceMessageHandler[]
                                         {
                                             new ExternalMessageRouteRegistrationHandler(externalRoutingTable, logger),
                                             new InternalMessageRouteRegistrationHandler(clusterMonitor, internalRoutingTable, logger),
                                             new MessageRouteDiscoveryHandler(internalRoutingTable, clusterMonitor),
                                             new MessageRouteUnregistrationHandler(externalRoutingTable),
                                             new RoutesRegistrationRequestHandler(clusterMonitor, internalRoutingTable),
                                             new RouteUnregistrationHandler(externalRoutingTable)
                                         };
            var messageTracer = new MessageTracer(logger);
            return new MessageRouter(socketFactory,
                                     internalRoutingTable,
                                     externalRoutingTable,
                                     routerConfiguration,
                                     clusterMonitor,
                                     messageTracer,
                                     serviceMessageHandlers,
                                     logger);
        }

        public IMessageHub CreateMessageHub(MessageHubConfiguration messageHubConfiguration,
                                            ILogger logger)
        {
            var messageTracer = new Client.MessageTracer(logger);
            var callbackHandlerStack = new CallbackHandlerStack();

            return new MessageHub(socketFactory,
                                  callbackHandlerStack,
                                  messageHubConfiguration,
                                  messageTracer,
                                  logger);
        }

        public IActorHost CreateActorHost(RouterConfiguration routerConfiguration,
                                          ILogger logger)
        {
            var messageTracer = new Actors.MessageTracer(logger);
            var actorRegistrationsQueue = new AsyncQueue<IActor>();
            var asyncQueue = new AsyncQueue<AsyncMessageContext>();
            var actorHandlerMap = new ActorHandlerMap();

            return new ActorHost(socketFactory,
                                 actorHandlerMap,
                                 asyncQueue,
                                 actorRegistrationsQueue,
                                 routerConfiguration,
                                 messageTracer,
                                 logger);
        }
    }
}