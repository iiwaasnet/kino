using System.Collections.Generic;
using System.Linq;
using Autofac;
using kino;
using kino.Configuration;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
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

            builder.Register(c => c.Resolve<IConfigProvider>().GetConfiguration<KinoConfiguration>())
                   .As<KinoConfiguration>()
                   .SingleInstance();

            builder.Register(c => new LoggerFactory(c.ResolveOptional<IEnumerable<ILoggerProvider>>()
                                                    ?? Enumerable.Empty<ILoggerProvider>())
                                 .CreateLogger("default"))
                   .As<ILogger>()
                   .SingleInstance();

            builder.RegisterType<NLogLoggerProvider>()
                   .As<ILoggerProvider>()
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