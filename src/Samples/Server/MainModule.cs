using Autofac;
using kino;
using kino.Actors;
using kino.Configuration;
using kino.Core.Diagnostics;
using Server.Actors;
using TypedConfigProvider;

namespace Server
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new Logger("default"))
                   .As<ILogger>()
                   .SingleInstance();

            builder.RegisterType<RevertStringActor>()
                   .As<IActor>();

            builder.RegisterType<GroupCharsActor>()
                   .As<IActor>();

            builder.Register(c => new ConfigProvider(c.Resolve<IConfigTargetProvider>(), "config"))
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<CoreAppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<KinoConfiguration>())
                   .As<KinoConfiguration>()
                   .SingleInstance();

            builder.Register(c => new DependencyResolver(c))
                   .As<IDependencyResolver>()
                   .SingleInstance();

            builder.Register(c => new kino.kino(c.Resolve<IDependencyResolver>()))
                   .AsSelf()
                   .SingleInstance();
        }
    }
}