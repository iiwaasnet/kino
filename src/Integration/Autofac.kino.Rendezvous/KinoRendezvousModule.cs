using kino.Connectivity;
using kino.Consensus;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics.Performance;
using kino.Messaging;
using kino.Rendezvous;
using kino.Rendezvous.Configuration;
using kino.Security;

namespace Autofac.kino.Rendezvous
{
    public class KinoRendezvousModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
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

            builder.RegisterType<LocalSocketFactory>()
                   .As<ILocalSocketFactory>()
                   .SingleInstance();

            builder.RegisterType<InstanceNameResolver>()
                   .As<IInstanceNameResolver>()
                   .SingleInstance();

            builder.RegisterType<SynodConfigurationProvider>()
                   .As<ISynodConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<RendezvousServiceConfiguration>().Lease)
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<RendezvousServiceConfiguration>().Synod)
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<RendezvousServiceConfiguration>().Rendezvous)
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<RendezvousServiceConfiguration>().Socket)
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<RendezvousServiceConfiguration>().Partners)
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<PerformanceCounterManager<KinoPerformanceCounters>>()
                   .As<IPerformanceCounterManager<KinoPerformanceCounters>>()
                   .SingleInstance();

            builder.RegisterType<IntercomMessageHub>()
                   .As<IIntercomMessageHub>()
                   .SingleInstance();

            builder.RegisterType<BallotGenerator>()
                   .As<IBallotGenerator>()
                   .SingleInstance();

            builder.RegisterType<RoundBasedRegister>()
                   .As<IRoundBasedRegister>()
                   .SingleInstance();

            builder.RegisterType<LeaseProvider>()
                   .As<ILeaseProvider>()
                   .SingleInstance();

            builder.RegisterType<ProtobufMessageSerializer>()
                   .As<IMessageSerializer>()
                   .SingleInstance();

            builder.RegisterType<RendezvousConfigurationProvider>()
                   .As<IRendezvousConfigurationProvider>()
                   .SingleInstance();

            builder.RegisterType<NullSecurityProvider>()
                   .As<ISecurityProvider>()
                   .SingleInstance();

            builder.RegisterType<PartnerNetworkConnectorManager>()
                   .As<IPartnerNetworkConnectorManager>()
                   .SingleInstance();

            builder.RegisterType<PartnerNetworksConfigurationProvider>()
                   .As<IPartnerNetworksConfigurationProvider>()
                   .SingleInstance();

            builder.RegisterType<RendezvousService>()
                   .As<IRendezvousService>()
                   .SingleInstance();
        }
    }
}