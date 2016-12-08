using System;
using System.Security.Cryptography;
using kino.Actors;
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

namespace kino
{
    public partial class kino
    {
        private Func<IMessageHub> getMessageHub;
        private Func<bool, IMessageHub> createMessageHub;
        private IActorHostManager actorHostManager;
        private IMessageRouter messageRouter;

        private void Build()
        {
            var configurationProvider = new ConfigurationProvider(resolver.Resolve<ApplicationConfiguration>());
            var scaleOutSocketConfiguration = configurationProvider.GetScaleOutConfiguration();
            var clusterMembershipConfiguration = configurationProvider.GetClusterMembershipConfiguration();
            var clusterHealthMonitorConfiguration = configurationProvider.GetClusterHealthMonitorConfiguration();
            var heartBeatSenderConfiguration = configurationProvider.GetHeartBeatSenderConfiguration();
            var rendezvousEndpoints = configurationProvider.GetRendezvousEndpointsConfiguration();
            var socketFactory = new SocketFactory(resolver.Resolve<SocketConfiguration>());
            var internalRoutingTable = new InternalRoutingTable();
            var logger = resolver.Resolve<ILogger>();
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var localSocketFactory = new LocalSocketFactory();
            var routerLocalSocket = new LocalSocket<IMessage>();
            var internalRegistrationSocket = new LocalSocket<InternalRouteRegistration>();
            var instanceNameResolver = resolver.Resolve<IInstanceNameResolver>() ?? new InstanceNameResolver();
            var performanceCounterManager = new PerformanceCounterManager<KinoPerformanceCounters>(instanceNameResolver,
                                                                                                   logger);
            var securityProvider = new SecurityProvider(() => HMAC.Create("HMACMD5"),
                                                        resolver.Resolve<IDomainScopeResolver>(),
                                                        resolver.Resolve<IDomainPrivateKeyProvider>());
            var heartBeatSenderConfigurationManager = new HeartBeatSenderConfigurationManager(heartBeatSenderConfiguration);
            var configurationStorage = resolver.Resolve<IConfigurationStorage<RendezvousClusterConfiguration>>()
                                       ?? new RendezvousClusterConfigurationReadonlyStorage(rendezvousEndpoints);
            var rendezvousCluster = new RendezvousCluster(configurationStorage);

            var scaleoutConfigurationProvider = new ServiceLocator<ScaleOutConfigurationManager,
                                                    NullScaleOutConfigurationManager,
                                                    IScaleOutConfigurationManager>(clusterMembershipConfiguration,
                                                                                   new ScaleOutConfigurationManager(scaleOutSocketConfiguration),
                                                                                   new NullScaleOutConfigurationManager())
                .GetService();
            var clusterHealthMonitor = new ServiceLocator<ClusterHealthMonitor,
                                           NullClusterHealthMonitor,
                                           IClusterHealthMonitor>(clusterMembershipConfiguration,
                                                                  new ClusterHealthMonitor(socketFactory,
                                                                                           localSocketFactory,
                                                                                           securityProvider,
                                                                                           clusterHealthMonitorConfiguration,
                                                                                           routerLocalSocket,
                                                                                           logger),
                                                                  new NullClusterHealthMonitor())
                .GetService();

            var heartBeatSender = new ServiceLocator<HeartBeatSender,
                                      NullHeartBeatSender,
                                      IHeartBeatSender>(clusterMembershipConfiguration,
                                                        new HeartBeatSender(socketFactory,
                                                                            heartBeatSenderConfigurationManager,
                                                                            scaleoutConfigurationProvider,
                                                                            logger),
                                                        new NullHeartBeatSender())
                .GetService();
            var scaleOutListener = new ServiceLocator<ScaleOutListener,
                                       NullScaleOutListener,
                                       IScaleOutListener>(clusterMembershipConfiguration,
                                                          new ScaleOutListener(socketFactory,
                                                                               routerLocalSocket,
                                                                               scaleoutConfigurationProvider,
                                                                               securityProvider,
                                                                               performanceCounterManager,
                                                                               logger),
                                                          new NullScaleOutListener())
                .GetService();
            var autoDiscoverSender = new AutoDiscoverySender(rendezvousCluster,
                                                             scaleoutConfigurationProvider,
                                                             socketFactory,
                                                             performanceCounterManager,
                                                             securityProvider,
                                                             logger);
            var autoDiscoveryListener = new AutoDiscoveryListener(rendezvousCluster,
                                                                  socketFactory,
                                                                  scaleoutConfigurationProvider,
                                                                  clusterMembershipConfiguration,
                                                                  performanceCounterManager,
                                                                  routerLocalSocket,
                                                                  logger);
            var routeDiscovery = new RouteDiscovery(autoDiscoverSender,
                                                    scaleoutConfigurationProvider,
                                                    clusterMembershipConfiguration,
                                                    securityProvider,
                                                    logger);
            var clusterMonitor = new ServiceLocator<ClusterMonitor,
                                     NullClusterMonitor,
                                     IClusterMonitor>(clusterMembershipConfiguration,
                                                      new ClusterMonitor(scaleoutConfigurationProvider,
                                                                         autoDiscoverSender,
                                                                         autoDiscoveryListener,
                                                                         heartBeatSenderConfigurationManager,
                                                                         routeDiscovery,
                                                                         securityProvider),
                                                      new NullClusterMonitor())
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
                                                                                                      securityProvider,
                                                                                                      logger);
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
            messageRouter = new MessageRouter(socketFactory,
                                                  internalRoutingTable,
                                                  externalRoutingTable,
                                                  scaleoutConfigurationProvider,
                                                  clusterServices,
                                                  serviceMessageHandlers,
                                                  performanceCounterManager,
                                                  securityProvider,
                                                  routerLocalSocket,
                                                  internalRegistrationSocket,
                                                  internalMessageRouteRegistrationHandler,
                                                  logger);
            var actorHostFactory = new ActorHostFactory(securityProvider,
                                                        routerLocalSocket,
                                                        internalRegistrationSocket,
                                                        localSocketFactory,
                                                        logger);
            var callbackHandlerStack = new CallbackHandlerStack();
            createMessageHub = (keepLocal) => new MessageHub(callbackHandlerStack,
                                                             routerLocalSocket,
                                                             internalRegistrationSocket,
                                                             localSocketFactory,
                                                             scaleoutConfigurationProvider,
                                                             logger,
                                                             keepLocal);
            var messageHub = createMessageHub(false);
            getMessageHub = () => messageHub;
            actorHostManager = new ActorHostManager(actorHostFactory, logger);
        }
    }
}