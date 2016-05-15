using System;
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
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(3);

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

            if (!rendezvousService.Start(StartTimeout))
            {
                throw new Exception($"Failed starting RendezvousService after {StartTimeout.TotalMilliseconds} ms!");
            }
        }

        private void Stop()
            => rendezvousService?.Stop();
    }
}