using System.Collections.Generic;
using System.Linq;
using Autofac;
using kino.Rendezvous.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using TypedConfigProvider;

namespace kino.Rendezvous.Service
{
    public class MainModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ConfigProvider>()
                   .As<IConfigProvider>()
                   .SingleInstance();

            builder.Register(c => new LoggerFactory(c.ResolveOptional<IEnumerable<ILoggerProvider>>()
                                                    ?? Enumerable.Empty<ILoggerProvider>())
                                 .CreateLogger("default"))
                   .As<ILogger>()
                   .SingleInstance();

            builder.RegisterType<NLogLoggerProvider>()
                   .As<ILoggerProvider>()
                   .SingleInstance();

            builder.RegisterType<AppConfigTargetProvider>()
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