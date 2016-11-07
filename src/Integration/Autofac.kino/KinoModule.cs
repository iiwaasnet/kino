using System.Collections.Generic;
using kino.Actors;
using kino.Actors.Diagnostics;
using kino.Client;
using kino.Cluster;
using kino.Cluster.Configuration;
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
            RegisterLocalSockets(builder);

            builder.RegisterType<PerformanceCounterManager<KinoPerformanceCounters>>()
                   .As<IPerformanceCounterManager<KinoPerformanceCounters>>()
                   .SingleInstance();

            builder.RegisterType<LocalSocketFactory>()
                   .As<ILocalSocketFactory>()
                   .SingleInstance();

            builder.RegisterType<InstanceNameResolver>()
                   .As<IInstanceNameResolver>()
                   .SingleInstance();

            builder.RegisterType<MessageRouter>()
                   .As<IMessageRouter>()
                   .SingleInstance();

            builder.Register(c => new SocketFactory(c.ResolveOptional<SocketConfiguration>()))
                   .As<ISocketFactory>()
                   .SingleInstance();

            builder.RegisterType<InternalRoutingTable>()
                   .As<IInternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<ExternalRoutingTable>()
                   .As<IExternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<ClusterConnectivity>()
                   .As<IClusterConnectivity>()
                   .SingleInstance();

            builder.RegisterType<AutoDiscoverySender>()
                   .As<IAutoDiscoverySender>()
                   .SingleInstance();

            builder.RegisterType<AutoDiscoveryListener>()
                   .As<IAutoDiscoveryListener>()
                   .SingleInstance();

            builder.RegisterType<RouteDiscovery>()
                   .As<IRouteDiscovery>()
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

            builder.RegisterType<RendezvousClusterConfigurationReadonlyStorage>()
                   .As<IConfigurationStorage<RendezvousClusterConfiguration>>()
                   .SingleInstance();

            builder.RegisterType<NullSecurityProvider>()
                   .As<ISecurityProvider>()
                   .SingleInstance();

            builder.RegisterType<ScaleOutConfigurationManager>()
                   .As<IScaleOutConfigurationProvider>()
                   .As<IScaleOutConfigurationManager>()
                   .SingleInstance();
        }

        private void RegisterLocalSockets(ContainerBuilder builder)
        {
            builder.RegisterType<LocalSocket<IMessage>>()
                   .As<ILocalSocket<IMessage>>()
                   .SingleInstance();

            builder.RegisterType<LocalSocket<InternalRouteRegistration>>()
                   .As<ILocalSendingSocket<InternalRouteRegistration>>()
                   .As<ILocalReceivingSocket<InternalRouteRegistration>>()
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

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRouterConfiguration())
                   .As<RouterConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRendezvousEndpointsConfiguration())
                   .As<IEnumerable<RendezvousEndpoint>>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetScaleOutConfiguration())
                   .As<ScaleOutSocketConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterMembershipConfiguration())
                   .As<ClusterMembershipConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterHealthMonitorConfiguration())
                   .As<ClusterHealthMonitorConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetHeartBeatSenderConfiguration())
                   .As<HeartBeatSenderConfiguration>()
                   .SingleInstance();
        }

        private void RegisterFrameworkActors(ContainerBuilder builder)
        {
            builder.RegisterType<KnownMessageRoutesActor>()
                   .As<IActor>()
                   .SingleInstance();

            builder.RegisterType<ExceptionHandlerActor>()
                   .As<IActor>()
                   .SingleInstance();
        }

        private void RegisterServiceMessageHandlers(ContainerBuilder builder)
        {
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
                   .As<InternalMessageRouteRegistrationHandler>()
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