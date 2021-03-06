﻿using kino.Actors;
using kino.Actors.Diagnostics;
using kino.Actors.Internal;
using kino.Client;
using kino.Cluster;
using kino.Cluster.Configuration;
using kino.Configuration;
using kino.Connectivity;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Routing;
using kino.Routing.ServiceMessageHandlers;
using kino.Security;

namespace Autofac.kino
{
    public class KinoModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            RegisterServiceMessageHandlers(builder);
            RegisterFrameworkActors(builder);
            RegisterConfigurations(builder);
            RegisterClusterServices(builder);

            builder.RegisterType<InstanceNameResolver>()
                   .As<IInstanceNameResolver>()
                   .SingleInstance();

            builder.RegisterType<PerformanceCounterManager<KinoPerformanceCounters>>()
                   .As<IPerformanceCounterManager<KinoPerformanceCounters>>()
                   .SingleInstance();

            builder.RegisterType<LocalSocketFactory>()
                   .As<ILocalSocketFactory>()
                   .SingleInstance();

            builder.RegisterType<MessageRouter>()
                   .As<IMessageRouter>()
                   .SingleInstance();
#if NET47
            builder.RegisterType<MessageWireFormatterV5>()
                   .As<IMessageWireFormatter>()
                   .SingleInstance();
#else
            builder.RegisterType<MessageWireFormatterV6_1>()
                   .As<IMessageWireFormatter>()
                   .SingleInstance();
#endif
            builder.RegisterType<SocketFactory>()
                   .As<ISocketFactory>()
                   .SingleInstance();

            builder.RegisterType<InternalRoutingTable>()
                   .As<IInternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<ExternalRoutingTable>()
                   .As<IExternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<RoundRobinDestinationList>()
                   .As<IRoundRobinDestinationList>()
                   .SingleInstance();

            builder.RegisterType<AutoDiscoverySender>()
                   .As<IAutoDiscoverySender>()
                   .SingleInstance();

            builder.RegisterType<AutoDiscoveryListener>()
                   .As<IAutoDiscoveryListener>()
                   .SingleInstance();

            builder.RegisterType<RendezvousCluster>()
                   .As<IRendezvousCluster>()
                   .SingleInstance();

            builder.RegisterType<CallbackHandlerStack>()
                   .As<ICallbackHandlerStack>()
                   .SingleInstance();

            builder.RegisterType<MessageHub>()
                   .As<IMessageHub>()
                   .SingleInstance();

            builder.RegisterType<ActorHostManager>()
                   .As<IActorHostManager>()
                   .SingleInstance();

            builder.RegisterType<ActorHostFactory>()
                   .As<IActorHostFactory>()
                   .SingleInstance();

            builder.RegisterType<NullSecurityProvider>()
                   .As<ISecurityProvider>()
                   .SingleInstance();
        }

        private static void RegisterClusterServices(ContainerBuilder builder)
        {
            RegisterScaleOutConfigurationManager(builder);
            RegisterClusterMonitor(builder);
            RegisterRouteDiscovery(builder);
            RegisterScaleOutListener(builder);
            RegisterHeartBeatSender(builder);
            RegisterClusterHealthMonitor(builder);
            builder.RegisterType<ClusterServices>()
                   .As<IClusterServices>()
                   .SingleInstance();
        }

        private static void RegisterClusterHealthMonitor(ContainerBuilder builder)
        {
            builder.RegisterType<ConnectedPeerRegistry>()
                   .As<IConnectedPeerRegistry>()
                   .SingleInstance();

            builder.RegisterType<NullClusterHealthMonitor>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<ClusterHealthMonitor>()
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => new ServiceLocator<IClusterHealthMonitor>(c.Resolve<ClusterMembershipConfiguration>(),
                                                                            c.Resolve<ClusterHealthMonitor>,
                                                                            c.Resolve<NullClusterHealthMonitor>)
                                .GetService())
                   .As<IClusterHealthMonitor>()
                   .SingleInstance();
        }

        private static void RegisterHeartBeatSender(ContainerBuilder builder)
        {
            builder.RegisterType<NullHeartBeatSender>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<HeartBeatSender>()
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => new ServiceLocator<IHeartBeatSender>(c.Resolve<ClusterMembershipConfiguration>(),
                                                                       c.Resolve<HeartBeatSender>,
                                                                       c.Resolve<NullHeartBeatSender>)
                                .GetService())
                   .As<IHeartBeatSender>()
                   .SingleInstance();
        }

        private static void RegisterScaleOutListener(ContainerBuilder builder)
        {
            builder.RegisterType<NullScaleOutListener>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<ScaleOutListener>()
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => new ServiceLocator<IScaleOutListener>(c.Resolve<ClusterMembershipConfiguration>(),
                                                                        c.Resolve<ScaleOutListener>,
                                                                        c.Resolve<NullScaleOutListener>)
                                .GetService())
                   .As<IScaleOutListener>()
                   .SingleInstance();
        }

        private static void RegisterClusterMonitor(ContainerBuilder builder)
        {
            builder.RegisterType<NullClusterMonitor>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<ClusterMonitor>()
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => new ServiceLocator<IClusterMonitor>(c.Resolve<ClusterMembershipConfiguration>(),
                                                                      c.Resolve<ClusterMonitor>,
                                                                      c.Resolve<NullClusterMonitor>)
                                .GetService())
                   .As<IClusterMonitor>()
                   .SingleInstance();
        }

        private static void RegisterRouteDiscovery(ContainerBuilder builder)
        {
            builder.RegisterType<RouteDiscovery>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<NullRouteDiscovery>()
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => new ServiceLocator<IRouteDiscovery>(c.Resolve<ClusterMembershipConfiguration>(),
                                                                      c.Resolve<RouteDiscovery>,
                                                                      c.Resolve<NullRouteDiscovery>)
                                .GetService())
                   .As<IRouteDiscovery>()
                   .SingleInstance();
        }

        private static void RegisterScaleOutConfigurationManager(ContainerBuilder builder)
        {
            builder.RegisterType<NullScaleOutConfigurationManager>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<ScaleOutConfigurationManager>()
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => new ServiceLocator<IScaleOutConfigurationManager>(c.Resolve<ClusterMembershipConfiguration>(),
                                                                                    c.Resolve<ScaleOutConfigurationManager>,
                                                                                    c.Resolve<NullScaleOutConfigurationManager>)
                                .GetService())
                   .As<IScaleOutConfigurationProvider>()
                   .As<IScaleOutConfigurationManager>()
                   .SingleInstance();
        }

        private void RegisterConfigurations(ContainerBuilder builder)
        {
            builder.RegisterType<ConfigurationProvider>()
                   .As<IConfigurationProvider>()
                   .SingleInstance();

            builder.RegisterType<HeartBeatSenderConfigurationManager>()
                   .As<IHeartBeatSenderConfigurationProvider>()
                   .As<IHeartBeatSenderConfigurationManager>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRendezvousEndpointsConfiguration())
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetScaleOutConfiguration())
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterMembershipConfiguration())
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterHealthMonitorConfiguration())
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetHeartBeatSenderConfiguration())
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetSocketConfiguration())
                   .AsSelf()
                   .SingleInstance();
        }

        private void RegisterFrameworkActors(ContainerBuilder builder)
        {
            builder.RegisterType<ExceptionHandlerActor>()
                   .As<IActor>()
                   .SingleInstance();

            builder.RegisterType<MessageRoutesActor>()
                   .As<IActor>()
                   .SingleInstance();
        }

        private void RegisterServiceMessageHandlers(ContainerBuilder builder)
        {
            builder.RegisterType<ServiceMessageHandlerRegistry>()
                   .As<IServiceMessageHandlerRegistry>()
                   .SingleInstance();

            builder.RegisterType<NodeRoutesRegistrar>()
                   .As<INodeRoutesRegistrar>()
                   .SingleInstance();

            builder.RegisterType<ClusterMessageRoutesRequestHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<PingHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<ExternalMessageRouteRegistrationHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<InternalMessageRouteRegistrationHandler>()
                   .As<IInternalMessageRouteRegistrationHandler>()
                   .SingleInstance();

            builder.RegisterType<MessageRouteDiscoveryHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<MessageRouteUnregistrationHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<NodeMessageRoutesRequestHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<NodeUnregistrationHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();

            builder.RegisterType<UnreachableNodeUnregistrationHandler>()
                   .As<IServiceMessageHandler>()
                   .SingleInstance();
        }
    }
}