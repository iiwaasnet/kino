using Autofac;
using kino.Connectivity;
using kino.Diagnostics;
using kino.Framework;
using kino.Messaging;
using kino.Sockets;

namespace kino
{
    public class MainModule : Module
    {
        public const string DefaultLogger = "default";

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageRouter>()
                   .As<IMessageRouter>()
                   .SingleInstance();

            builder.RegisterType<InternalRoutingTable>()
                   .As<IInternalRoutingTable>()
                   .SingleInstance();

            builder.Register(c => new Logger(DefaultLogger))
                   .As<ILogger>()
                   .SingleInstance();

            builder.RegisterType<ExpirableItemScheduledCollection<CorrelationId>>()
                   .As<IExpirableItemCollection<CorrelationId>>();

            builder.RegisterType<ExternalRoutingTable>()
                   .As<IExternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<ClusterMonitorProvider>()
                   .As<IClusterMonitorProvider>()
                   .SingleInstance();

            builder.RegisterType<ClusterMembership>()
                   .As<IClusterMembership>()
                   .SingleInstance();

            builder.RegisterType<ClusterMonitor>()
                   .As<IClusterMonitor>()
                   .SingleInstance();

            builder.Register(c => new SocketFactory(c.ResolveOptional<SocketConfiguration>()))
                   .As<ISocketFactory>()
                   .SingleInstance();

            builder.RegisterType<RendezvousCluster>()
                   .As<IRendezvousCluster>()
                   .SingleInstance();

            builder.RegisterType<RendezvousClusterConfigurationReadonlyStorage>()
                   .As<IConfigurationStorage<RendezvousClusterConfiguration>>()
                   .SingleInstance();

            builder.RegisterType<MessageTracer>()
                   .As<IMessageTracer>()
                   .SingleInstance();
        }
    }
}