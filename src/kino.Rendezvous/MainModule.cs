using Autofac;
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
            builder.RegisterModule(new Consensus.MainModule());

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

            builder.RegisterType<LeaseConfigurationProvider>()
                   .As<ILeaseConfigurationProvider>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<ILeaseConfigurationProvider>().GetConfiguration())
                   .As<LeaseConfiguration>()
                   .SingleInstance();

            builder.RegisterType<RendezvousConfigurationProvider>()
                   .As<IRendezvousConfigurationProvider>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<IRendezvousConfigurationProvider>().GetConfiguration())
                   .As<RendezvousConfiguration>()
                   .SingleInstance();
        }
    }
}