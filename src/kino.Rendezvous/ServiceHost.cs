using System;
using kino.Consensus.Configuration;
using kino.Core.Diagnostics;
using kino.Core.Sockets;
using kino.Rendezvous.Configuration;
using Topshelf;
using Topshelf.HostConfigurators;

namespace kino.Rendezvous
{
    public class ServiceHost
    {
        private readonly IRendezvousService rendezvousService;
        private readonly RendezvousConfiguration config;
        private readonly ILogger logger;

        public ServiceHost(IRendezvousService rendezvousService, RendezvousConfiguration config, ILogger logger)
        {
            this.logger = logger;
            this.config = config;
            this.rendezvousService = rendezvousService;
        }

        public ServiceHost(LeaseConfiguration leaseConfiguration,
                           SocketConfiguration socketConfiguration,
                           RendezvousConfiguration rendezvousConfiguration,
                           ApplicationConfiguration applicationConfiguration,
                           ILogger logger)
        {
            this.logger = logger;
            config = rendezvousConfiguration;
            rendezvousService = new Composer().BuildRendezvousService(leaseConfiguration,
                                                                     socketConfiguration,
                                                                     rendezvousConfiguration,
                                                                     applicationConfiguration,
                                                                     logger);
        }

        public void Run()
            => HostFactory.Run(CreateServiceConfiguration);

        private void CreateServiceConfiguration(HostConfigurator x)
        {
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
            }
            catch (Exception err)
            {
                logger.Error(err);
            }
        }

        private IRendezvousService CreateServiceInstance()
            => rendezvousService;

        private void AssertLoggerIsSet(ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }
        }
    }
}