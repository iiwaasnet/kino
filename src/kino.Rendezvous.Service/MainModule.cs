using Autofac;
using kino.Core.Diagnostics;
using kino.Rendezvous.Configuration;
using TypedConfigProvider;

namespace kino.Rendezvous.Service
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.Register(c => new Logger("default"))
                   .As<ILogger>()
                   .SingleInstance();

            builder.Register(c => new ConfigProvider(c.Resolve<IConfigTargetProvider>(), "config"))
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.RegisterType<CoreAppConfigTargetProvider>()
                   .As<IConfigTargetProvider>()
                   .SingleInstance();

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<RendezvousServiceConfiguration>())
                   .As<RendezvousServiceConfiguration>()
                   .SingleInstance();

            builder.Register(c => new DependencyResolver(c))
                   .As<IDependencyResolver>()
                   .SingleInstance();

            builder.Register(c => new Rendezvous(c.Resolve<IDependencyResolver>()))
                   .AsSelf()
                   .SingleInstance();

            builder.Register(c => c.Resolve<Rendezvous>().GetRendezvousService())
                   .As<IRendezvousService>()
                   .SingleInstance();
        }
    }
}