using Autofac;
using rawf.Connectivity;
using rawf.Diagnostics;
using rawf.Framework;
using rawf.Messaging;
using rawf.Sockets;

namespace rawf
{
    public class MainModule : Module
    {
        public const string FileLogger = "fileLogger";

        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MessageRouter>()
                   .As<IMessageRouter>()
                   .SingleInstance();

            builder.RegisterType<InternalRoutingTable>()
                   .As<IInternalRoutingTable>()
                   .SingleInstance();

            builder.Register(c => new Logger(FileLogger))
                   .As<ILogger>()
                   .SingleInstance();

            builder.Register(c => new ExpirableItemScheduledCollection<CorrelationId>(c.Resolve<IExpirableItemCollectionConfiguration>().EvaluationInterval))
                   .As<IExpirableItemCollection<CorrelationId>>();

            builder.RegisterType<ExternalRoutingTable>()
                   .As<IExternalRoutingTable>()
                   .SingleInstance();

            builder.RegisterType<ClusterConfiguration>()
                   .As<IClusterConfiguration>()
                   .SingleInstance();

            builder.RegisterType<ClusterMonitor>()
                   .As<IClusterMonitor>()
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