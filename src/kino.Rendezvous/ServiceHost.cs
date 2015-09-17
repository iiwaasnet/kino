using System;
using Autofac;
using Autofac.Configuration;
using kino.Diagnostics;
using kino.Rendezvous.Configuration;
using Topshelf;
using Topshelf.HostConfigurators;

namespace kino.Rendezvous
{
    public class ServiceHost
    {
        private readonly IContainer container;
        private readonly ILogger logger;

        public ServiceHost()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule(new MainModule());
            builder.RegisterModule(new ConfigurationSettingsReader("autofac"));
            container = builder.Build();
            logger = container.Resolve<ILogger>();

            AssertLoggerIsSet(logger);
        }

        public void Run()
            => HostFactory.Run(CreateServiceConfiguration);

        private void CreateServiceConfiguration(HostConfigurator x)
        {
            

            var config = container.Resolve<RendezvousConfiguration>();

            x.Service<IRendezvousService>(s =>
                                          {
                                              s.ConstructUsing(_ => CreateServiceInstance());
                                              s.WhenStarted(rs => rs.Start());
                                              s.WhenStopped(ServiceStop);
                                          });
            x.RunAsPrompt();
            x.SetServiceName(config.ServiceName);
            x.SetDisplayName(config.ServiceName);
            x.SetDescription($"{config.ServiceName} Service");
        }

        private void ServiceStop(IRendezvousService rs)
        {
            try
            {
                rs.Stop();
                container.Dispose();
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private IRendezvousService CreateServiceInstance()
            => container.Resolve<IRendezvousService>();

        private void AssertLoggerIsSet(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
        }
    }
}