using System.Collections.Generic;
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

namespace kino.Integration
{
    public class ComponentsContainer
    {
        private readonly IDomainPrivateKeyProvider domainPrivateKeyProvider;
        private readonly IDomainScopeResolver domainScopeResolver;
        private readonly ILogger logger;
        private readonly ConfigurationProvider configurationProvider;
        private IMessageRouter messageRouter;
        private ActorHostManager actorHostManager;
        private MessageHub messageHub;

        public ComponentsContainer(ApplicationConfiguration appConfig,
                                   IDomainPrivateKeyProvider domainPrivateKeyProvider,
                                   IDomainScopeResolver domainScopeResolver,
                                   ILogger logger)
        {
            this.domainPrivateKeyProvider = domainPrivateKeyProvider;
            this.domainScopeResolver = domainScopeResolver;
            this.logger = logger;
            configurationProvider = new ConfigurationProvider(appConfig);
        }

        private void Build()
        {
            var socketFactory = new SocketFactory();
            var perfCountersManager = CreatePerfCountersManager();
            var localSocketFactory = new LocalSocketFactory();
            var routerLocalSocket = localSocketFactory.Create<IMessage>();

            var internalRoutingTable = new InternalRoutingTable();
            var externalRoutingTable = new ExternalRoutingTable(logger);
            var scaleOutConfigurationManager = new ScaleOutConfigurationManager(configurationProvider.GetRouterConfiguration(),
                                                                                configurationProvider.GetScaleOutConfiguration());

            var rendezvousConfigurationStorage = new RendezvousClusterConfigurationReadonlyStorage(configurationProvider.GetRendezvousEndpointsConfiguration());
            var rendezvousCluster = new RendezvousCluster(rendezvousConfigurationStorage);
            var securityProider = new SecurityProvider(() => HMAC.Create("HMACMD5"),
                                                       domainScopeResolver,
                                                       domainPrivateKeyProvider);
            var autoDiscoverSender = new AutoDiscoverySender(rendezvousCluster,
                                                             scaleOutConfigurationManager,
                                                             socketFactory,
                                                             perfCountersManager,
                                                             securityProider,
                                                             logger);
            var autoDiscoverListener = new AutoDiscoveryListener(rendezvousCluster,
                                                                 socketFactory,
                                                                 scaleOutConfigurationManager,
                                                                 autoDiscoverSender,
                                                                 configurationProvider.GetClusterMembershipConfiguration(),
                                                                 perfCountersManager,
                                                                 securityProider,
                                                                 routerLocalSocket,
                                                                 logger);
            var routeDiscovery = new RouteDiscovery(autoDiscoverSender,
                                                    scaleOutConfigurationManager,
                                                    configurationProvider.GetClusterMembershipConfiguration(),
                                                    securityProider,
                                                    logger);
            var heartBeatSenderConfigurationManager = new HeartBeatSenderConfigurationManager(configurationProvider.GetHeartBeatSenderConfiguration());
            var clusterConnectivity = new ClusterConnectivity(configurationProvider.GetClusterMembershipConfiguration(),
                                                              scaleOutConfigurationManager,
                                                              autoDiscoverSender,
                                                              autoDiscoverListener,
                                                              routeDiscovery,
                                                              socketFactory,
                                                              routerLocalSocket,
                                                              scaleOutConfigurationManager,
                                                              securityProider,
                                                              perfCountersManager,
                                                              logger,
                                                              heartBeatSenderConfigurationManager,
                                                              localSocketFactory,
                                                              configurationProvider.GetClusterHealthMonitorConfiguration());
            var internalRegistrationsSocket = localSocketFactory.Create<InternalRouteRegistration>();
            var internalRegistrationsHandler = new InternalMessageRouteRegistrationHandler(clusterConnectivity,
                                                                                           internalRoutingTable,
                                                                                           securityProider,
                                                                                           logger);
            messageRouter = new MessageRouter(socketFactory,
                                              internalRoutingTable,
                                              externalRoutingTable,
                                              scaleOutConfigurationManager,
                                              clusterConnectivity,
                                              CreateServiceMessageHandlers(clusterConnectivity,
                                                                           externalRoutingTable,
                                                                           internalRoutingTable,
                                                                           securityProider),
                                              perfCountersManager,
                                              securityProider,
                                              routerLocalSocket,
                                              internalRegistrationsSocket,
                                              internalRegistrationsHandler,
                                              logger);

            actorHostManager = new ActorHostManager(securityProider,
                                                    perfCountersManager,
                                                    routerLocalSocket,
                                                    internalRegistrationsSocket,
                                                    localSocketFactory,
                                                    logger);
            var callbackHandlerStack = new CallbackHandlerStack();

            messageHub = new MessageHub(callbackHandlerStack,
                                        perfCountersManager,
                                        routerLocalSocket,
                                        internalRegistrationsSocket,
                                        localSocketFactory,
                                        logger,
                                        false);
        }

        private IEnumerable<IServiceMessageHandler> CreateServiceMessageHandlers(IClusterConnectivity clusterConnectivity,
                                                                                 IExternalRoutingTable externalRoutingTable,
                                                                                 IInternalRoutingTable internalRoutingTable,
                                                                                 ISecurityProvider securityProvider)
        {
            yield return new UnreachableNodeUnregistrationHandler(clusterConnectivity, externalRoutingTable);
            yield return new ClusterMessageRoutesRequestHandler(clusterConnectivity, internalRoutingTable, securityProvider);
            yield return new ExternalMessageRouteRegistrationHandler(externalRoutingTable, securityProvider, clusterConnectivity, logger);
            yield return new MessageRouteDiscoveryHandler(clusterConnectivity, internalRoutingTable, securityProvider, logger);
            yield return new MessageRouteUnregistrationHandler(clusterConnectivity, externalRoutingTable, securityProvider, logger);
            yield return new NodeUnregistrationHandler(clusterConnectivity, externalRoutingTable, securityProvider);
            yield return new NodeMessageRoutesRequestHandler(clusterConnectivity, internalRoutingTable, securityProvider);
            yield return new PingHandler();
        }

        private IPerformanceCounterManager<KinoPerformanceCounters> CreatePerfCountersManager()
        {
            var instanceNamedResolver = new InstanceNameResolver();
            var perfCountersManager = new PerformanceCounterManager<KinoPerformanceCounters>(instanceNamedResolver, logger);

            return perfCountersManager;
        }
    }
}