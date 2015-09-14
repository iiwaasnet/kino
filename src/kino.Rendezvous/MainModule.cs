using Autofac;
using kino.Rendezvous.Configuration;
using kino.Rendezvous.Consensus;
using kino.Sockets;
using TypedConfigProvider;

namespace kino.Rendezvous
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new kino.MainModule());
            builder.RegisterModule(new Consensus.ConsensusModule());

            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<ApplicationConfiguration>())
                   .As<ApplicationConfiguration>()
                   .SingleInstance();

            builder.RegisterType<RendezvousService>()
                   .As<IRendezvousService>()
                   .SingleInstance();

            builder.RegisterType<SocketFactory>()
                   .As<ISocketFactory>()
                   .SingleInstance();

            builder.RegisterType<SynodConfigurationProvider>()
                   .As<ISynodConfigurationProvider>()
                   .SingleInstance();

            builder.RegisterType<ConfigurationProvider>()
                   .As<IConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetLeaseConfiguration())
                   .As<LeaseConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRendezvousConfiguration())
                   .As<RendezvousConfiguration>()
                   .SingleInstance();
        }
    }
}