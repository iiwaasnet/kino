using WindowsServiceHost;
using Autofac;
using kino.Core.Diagnostics;
using kino.Core.Sockets;
using kino.Rendezvous;
using kino.Rendezvous.Configuration;

namespace Rendezvous
{
    public class ServiceHost : WindowsService
    {
        private IRendezvousService rendezvousService;

        protected override ServiceConfiguration GetServiceConfiguration()
            => new ServiceConfiguration
               {
                   ServiceName = "kino.Rendezvous",
                   DisplayName = "kino.Rendezvous",
                   OnStart = Start,
                   OnStop = Stop
               };

        private void Start()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<RendezvousModule>();
            var container = builder.Build();

            rendezvousService = new Composer().BuildRendezvousService(container.ResolveOptional<SocketConfiguration>(),
                                                                      container.Resolve<ApplicationConfiguration>(),
                                                                      container.Resolve<ILogger>());

            rendezvousService.Start();
        }

        private void Stop()
            => rendezvousService?.Stop();
    }
}