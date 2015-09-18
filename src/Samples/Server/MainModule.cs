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

            builder.RegisterType<ConfigurationProvider>()
                   .As<IConfigurationProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRouterConfiguration())
                   .As<RouterConfiguration>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetRendezvousEndpointsConfiguration())
                   .As<IEnumerable<RendezvousEndpoints>>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigurationProvider>().GetClusterTimingConfiguration())
                   .As<ClusterMembershipConfiguration>()
                   .SingleInstance();
        }
    }
}