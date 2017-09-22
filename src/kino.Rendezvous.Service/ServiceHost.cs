#if NET47
using System;
using System.Diagnostics;
using WindowsServiceHost;
using Autofac;
using kino.Core.Diagnostics.Performance;

namespace kino.Rendezvous.Service
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
                   OnStop = Stop,
                   Installers = new[] {new PerformanceCounterInstaller()}
               };

        private void Start()
        {
            var builder = new ContainerBuilder();
            builder.RegisterModule<MainModule>();
            var container = builder.Build();

            rendezvousService = container.Resolve<IRendezvousService>();

            if (!rendezvousService.Start(StartTimeout))
            {
                throw new Exception($"Failed starting RendezvousService after {StartTimeout.TotalMilliseconds} ms!");
            }
        }

        private void Stop()
            => rendezvousService?.Stop();
    }
}
#endif