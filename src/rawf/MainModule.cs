using Autofac;
using rawf.Connectivity;
using rawf.Framework;
using rawf.Messaging;
using rawf.Sockets;

namespace rawf
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageRouter>()
                   .As<IMessageRouter>()
                   .SingleInstance();

            builder.RegisterType<InternalRoutingTable>()
                   .As<IInternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<DelayedCollection<CorrelationId>>()
                   .As<IDelayedCollection<CorrelationId>>();

            builder.RegisterType<ExternalRoutingTable>()
                   .As<IExternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<ClusterConfiguration>()
                   .As<IClusterConfiguration>()
                   .SingleInstance();

            builder.RegisterType<ClusterConfigurationMonitor>()
                   .As<IClusterConfigurationMonitor>()
                   .SingleInstance();

            builder.RegisterType<SocketFactory>()
                   .As<ISocketFactory>()
                   .SingleInstance();

            builder.RegisterType<RendezvousConfiguration>()
                   .As<IRendezvousConfiguration>()
                   .SingleInstance();
        }
    }
}