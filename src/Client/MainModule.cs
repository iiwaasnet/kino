using System.Collections.Generic;
using Autofac;
using rawf.Client;
using rawf.Connectivity;
using rawf.Framework;
using TypedConfigProvider;

namespace Client
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new rawf.Client.MainModule());

            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<ApplicationConfiguration>())
                   .As<ApplicationConfiguration>()
                   .SingleInstance();

            builder.RegisterType<MessageHubConfigurationProvider>()
                   .As<IMessageHubConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IMessageHubConfigurationProvider>().GetConfiguration())
                   .As<IMessageHubConfiguration>()
                   .SingleInstance();

            builder.RegisterType<RouterConfigurationProvider>()
                   .As<IRouterConfigurationProvider>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<IRouterConfigurationProvider>().GetConfiguration())
                   .As<IRouterConfiguration>()
                   .SingleInstance();

            builder.RegisterType<ClusterConfigurationProvider>()
                   .As<IClusterConfigurationProvider>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<IClusterConfigurationProvider>().GetConfiguration())
                   .As<IClusterConfiguration>()
                   .SingleInstance();

            builder.RegisterType<ExpirableItemCollectionConfigurationProvider>()
                   .As<IExpirableItemCollectionConfigurationProvider>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<IExpirableItemCollectionConfigurationProvider>().GetConfiguration())
                   .As<IExpirableItemCollectionConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IInitialRendezvousServerConfiguration>().GetConfiguration())
                   .As<IEnumerable<RendezvousServerConfiguration>>()
                   .SingleInstance();

            builder.RegisterType<InitialRendezvousServerConfiguration>()
                   .As<IInitialRendezvousServerConfiguration>()
                   .SingleInstance();
        }
    }
}