using System.Collections.Generic;
using Autofac;
using kino.Actors;
using kino.Connectivity;
using Server.Actors;
using TypedConfigProvider;

namespace Server
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterModule(new kino.Actors.MainModule());

          builder.RegisterType<RevertStringActor>()
            .As<IActor>()
            .SingleInstance();

      builder.RegisterType<GroupCharsActor>()
            .As<IActor>()
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

            builder.RegisterType<RouterConfigurationProvider>()
                   .As<IRouterConfigurationProvider>()
                   .SingleInstance();
            builder.Register(c => c.Resolve<IRouterConfigurationProvider>().GetConfiguration())
                   .As<RouterConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IRendezvousEndpointsProvider>().GetConfiguration())
                   .As<IEnumerable<RendezvousEndpoints>>()
                   .SingleInstance();

            builder.RegisterType<RendezvousEndpointsProvider>()
                   .As<IRendezvousEndpointsProvider>()
                   .SingleInstance();

            builder.RegisterType<ClusterTimingConfigurationProvider>()
                   .As<IClusterTimingConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IClusterTimingConfigurationProvider>().GetConfiguration())
                   .As<ClusterTimingConfiguration>()
                   .SingleInstance();
        }
    }
}