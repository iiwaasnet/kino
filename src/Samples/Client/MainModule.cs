using Autofac;
using Autofac.kino;
using TypedConfigProvider;

namespace Client
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
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

            builder.RegisterLogger(c => new Logger("default"))
                   .SingleInstance();

            builder.RegisterMessageHubConfiguration(c => c.Resolve<IConfigurationProvider>().GetMessageHubConfiguration())
                   .SingleInstance();

            builder.RegisterRouterConfiguration(c => c.Resolve<IConfigurationProvider>().GetRouterConfiguration())
                   .SingleInstance();

            builder.RegisterRendezvousEndpoints(c => c.Resolve<IConfigurationProvider>().GetRendezvousEndpointsConfiguration())
                   .SingleInstance();

            builder.RegisterClusterMembershipConfiguration(c => c.Resolve<IConfigurationProvider>().GetClusterMembershipConfiguration())
                   .SingleInstance();
        }
    }
}