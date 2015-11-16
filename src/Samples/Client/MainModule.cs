using System.Collections.Generic;
using Autofac;
using kino.Client;
using kino.Core.Connectivity;
using kino.Core.Diagnostics;
using TypedConfigProvider;

namespace Client
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new Logger("default"))
                   .As<ILogger>()
                   .SingleInstance();

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

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetMessageHubConfiguration())
                   .As<MessageHubConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRouterConfiguration())
                   .As<RouterConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRendezvousEndpointsConfiguration())
                   .As<IEnumerable<RendezvousEndpoint>>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterMembershipConfiguration())
                   .As<ClusterMembershipConfiguration>()
                   .SingleInstance();
        }
    }
}