using System;

namespace rawf.Rendezvous
{
    public class RendezvousConfigurationProvider : IRendezvousConfigurationProvider
    {
        private readonly IRendezvousConfiguration config;

        public RendezvousConfigurationProvider(ApplicationConfiguration appConfig)
        {
            config = new RendezvousConfiguration
                     {
                         MulticastUri = new Uri(appConfig.BroadcastUri),
                         UnicastUri = new Uri(appConfig.UnicastUri),
                         PingInterval = appConfig.PingInterval
                     };
        }

        public IRendezvousConfiguration GetConfiguration()
            => config;
    }
}