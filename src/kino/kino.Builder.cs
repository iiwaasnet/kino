using System;
using System.Security.Cryptography;
using kino.Actors;
using kino.Actors.Internal;
using kino.Client;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;

#if NETCOREAPP2_1
using NetMQ;

#endif

namespace kino
{
    public partial class kino
    {
        public void Build(IDependencyResolver resolver)
        {
            this.resolver = resolver;
            Build();
        }

        public void Build()
        {
            AssertDependencyResolverSet();

#if NETCOREAPP2_1
            BufferPool.SetCustomBufferPool(new CustomBufferPool());
#endif

            var configurationProvider = new ConfigurationProvider(resolver.Resolve<KinoConfiguration>());
            var scaleOutSocketConfiguration = configurationProvider.GetScaleOutConfiguration();
            var clusterMembershipConfiguration = configurationProvider.GetClusterMembershipConfiguration();
            var clusterHealthMonitorConfiguration = configurationProvider.GetClusterHealthMonitorConfiguration();
            var heartBeatSenderConfiguration = configurationProvider.GetHeartBeatSenderConfiguration();
            var socketConfiguration = configurationProvider.GetSocketConfiguration();
            var rendezvousEndpoints = configurationProvider.GetRendezvousEndpointsConfiguration();
            var messageWireFormatter =
#if NETCOREAPP2_1
                resolver.Resolve<IMessageWireFormatter>() ?? new MessageWireFormatterV6_1();
#endif
#if NET47
                resolver.Resolve<IMessageWireFormatter>() ?? new MessageWireFormatterV5();
#endif
            var socketFactory = new SocketFactory(messageWireFormatter, socketConfiguration);
            var logger = resolver.Resolve<ILogger>();
            var roundRobinDestinationList = new RoundRobinDestinationList(logger);
            var internalRoutingTable = new InternalRoutingTable(roundRobinDestinationList);
            var externalRoutingTable = new ExternalRoutingTable(roundRobinDestinationList, logger);
            var localSocketFactory = new LocalSocketFactory();

            var instanceNameResolver = resolver.Resolve<IInstanceNameResolver>() ?? new InstanceNameResolver();
            var performanceCounterManager = new PerformanceCounterManager<KinoPerformanceCounters>(instanceNameResolver,
                                                                                                   logger);

            var hashCodeProvider = resolver.Resolve<Func<HMAC>>() ?? (() => HMAC.Create("HMACMD5"));

            var securityProvider = resolver.Resolve<ISecurityProvider>()
                                ?? new SecurityProvider(hashCodeProvider,
                                                        resolver.Resolve<IDomainScopeResolver>(),
                                                        resolver.Resolve<IDomainPrivateKeyProvider>());
            var heartBeatSenderConfigurationManager = new HeartBeatSenderConfigurationManager(heartBeatSenderConfiguration);
            var rendezvousCluster = new RendezvousCluster(rendezvousEndpoints);

            var scaleOutConfigurationProvider = new ServiceLocator<IScaleOutConfigurationManager>(clusterMembershipConfiguration,
                                                                                                  () => new ScaleOutConfigurationManager(scaleOutSocketConfiguration),
                                                                                                  () => new NullScaleOutConfigurationManager())
               .GetService();
            var connectedPeerRegistry = new ConnectedPeerRegistry(clusterHealthMonitorConfiguration);
            var clusterHealthMonitor = new ServiceLocator<IClusterHealthMonitor>(clusterMembershipConfiguration,
                                                                                 () => new ClusterHealthMonitor(socketFactory,
                                                                                                                localSocketFactory,
                                                                                                                securityProvider,
                                                                                                                connectedPeerRegistry,
                                                                                                                clusterHealthMonitorConfiguration,
                                                                                                                logger),
                                                                                 () => new NullClusterHealthMonitor())
               .GetService();

            var heartBeatSender = new ServiceLocator<IHeartBeatSender>(clusterMembershipConfiguration,
                                                                       () => new HeartBeatSender(socketFactory,
                                                                                                 heartBeatSenderConfigurationManager,
                                                                                                 scaleOutConfigurationProvider,
                                                                                                 logger),
                                                                       () => new NullHeartBeatSender())
               .GetService();
            var scaleOutListener = new ServiceLocator<IScaleOutListener>(clusterMembershipConfiguration,
                                                                         () => new ScaleOutListener(socketFactory,
                                                                                                    localSocketFactory,
                                                                                                    scaleOutConfigurationProvider,
                                                                                                    securityProvider,
                                                                                                    performanceCounterManager,
                                                                                                    logger),
                                                                         () => new NullScaleOutListener())
               .GetService();
            var autoDiscoverSender = new AutoDiscoverySender(rendezvousCluster,
                                                             socketFactory,
                                                             clusterMembershipConfiguration,
                                                             performanceCounterManager,
                                                             logger);
            var autoDiscoveryListener = new AutoDiscoveryListener(rendezvousCluster,
                                                                  socketFactory,
                                                                  localSocketFactory,
                                                                  scaleOutConfigurationProvider,
                                                                  clusterMembershipConfiguration,
                                                                  performanceCounterManager,
                                                                  logger);

            var routeDiscovery = new ServiceLocator<IRouteDiscovery>(clusterMembershipConfiguration,
                                                                     () => new RouteDiscovery(autoDiscoverSender,
                                                                                              scaleOutConfigurationProvider,
                                                                                              clusterMembershipConfiguration,
                                                                                              securityProvider,
                                                                                              logger),
                                                                     () => new NullRouteDiscovery())
               .GetService();
            var clusterMonitor = new ServiceLocator<IClusterMonitor>(clusterMembershipConfiguration,
                                                                     () => new ClusterMonitor(scaleOutConfigurationProvider,
                                                                                              autoDiscoverSender,
                                                                                              autoDiscoveryListener,
                                                                                              heartBeatSenderConfigurationManager,
                                                                                              routeDiscovery,
                                                                                              securityProvider,
                                                                                              clusterMembershipConfiguration,
                                                                                              logger),
                                                                     () => new NullClusterMonitor())
               .GetService();
            var clusterServices = new ClusterServices(clusterMonitor,
                                                      scaleOutListener,
                                                      heartBeatSender,
                                                      clusterHealthMonitor);
            var nodeRoutesRegistrar = new NodeRoutesRegistrar(clusterServices,
                                                              internalRoutingTable,
                                                              securityProvider);
            var internalMessageRouteRegistrationHandler = new InternalMessageRouteRegistrationHandler(clusterMonitor,
                                                                                                      internalRoutingTable,
                                                                                                      securityProvider);
            var serviceMessageHandlers = new IServiceMessageHandler[]
                                         {
                                             new ClusterMessageRoutesRequestHandler(securityProvider,
                                                                                    nodeRoutesRegistrar),
                                             new PingHandler(),
                                             new ExternalMessageRouteRegistrationHandler(externalRoutingTable,
                                                                                         securityProvider,
                                                                                         clusterHealthMonitor,
                                                                                         logger),
                                             new MessageRouteDiscoveryHandler(clusterMonitor,
                                                                              internalRoutingTable,
                                                                              securityProvider,
                                                                              logger),
                                             new MessageRouteUnregistrationHandler(clusterHealthMonitor,
                                                                                   externalRoutingTable,
                                                                                   securityProvider,
                                                                                   logger),
                                             new NodeMessageRoutesRequestHandler(securityProvider,
                                                                                 nodeRoutesRegistrar),
                                             new NodeUnregistrationHandler(clusterHealthMonitor,
                                                                           externalRoutingTable,
                                                                           securityProvider),
                                             new UnreachableNodeUnregistrationHandler(clusterHealthMonitor,
                                                                                      externalRoutingTable)
                                         };
            var serviceMessageHandlerRegistry = new ServiceMessageHandlerRegistry(serviceMessageHandlers);
            messageRouter = new MessageRouter(socketFactory,
                                              localSocketFactory,
                                              internalRoutingTable,
                                              externalRoutingTable,
                                              scaleOutConfigurationProvider,
                                              clusterServices,
                                              serviceMessageHandlerRegistry,
                                              performanceCounterManager,
                                              securityProvider,
                                              internalMessageRouteRegistrationHandler,
                                              roundRobinDestinationList,
                                              logger);
            var actorHostFactory = new ActorHostFactory(securityProvider,
                                                        localSocketFactory,
                                                        logger);
            var callbackHandlerStack = new CallbackHandlerStack();
            createMessageHub = keepLocal => new MessageHub(callbackHandlerStack,
                                                           localSocketFactory,
                                                           scaleOutConfigurationProvider,
                                                           securityProvider,
                                                           logger,
                                                           keepLocal);
            var messageHub = createMessageHub(false);
            getMessageHub = () => messageHub;
            actorHostManager = new ActorHostManager(actorHostFactory, logger);

            internalActorHostManager = new ActorHostManager(actorHostFactory, logger);
            var internalActor = new MessageRoutesActor(externalRoutingTable, internalRoutingTable, scaleOutConfigurationProvider);
            internalActorHostManager.AssignActor(internalActor);
            //
            isBuilt = true;
        }
    }
}