using Autofac;
using kino;
using kino.Configuration;
using kino.Core.Diagnostics;
using TypedConfigProvider;

namespace Client
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new ConfigProvider(c.Resolve<IConfigTargetProvider>(), "config"))
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<CoreAppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<KinoConfiguration>())
                   .As<KinoConfiguration>()
                   .SingleInstance();

            builder.Register(c => new Logger("default"))
                   .As<ILogger>()
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