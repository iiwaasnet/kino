using Autofac;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Rendezvous.Configuration;
using TypedConfigProvider;

namespace Rendezvous
{
    public class RendezvousModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.Register(c => new Logger("default"))
                   .As<ILogger>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<ApplicationConfiguration>())
                   .As<ApplicationConfiguration>()
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